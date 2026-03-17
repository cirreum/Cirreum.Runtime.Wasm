namespace Cirreum.Runtime.State;

using Cirreum.Runtime.Security;
using Cirreum.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates application initialization in two phases, coordinating Cirreum-controlled
/// services and app-registered <see cref="IInitializable"/> services while reporting
/// progress through <see cref="IActivityState"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Phase 1 — Cirreum-controlled (fixed order):</strong>
/// Application user loading via <see cref="IApplicationUserResolver"/> and profile
/// enrichment via <see cref="IUserProfileEnricher"/>. These run first, in a fixed
/// order, only when the user is authenticated and the respective services are registered.
/// </para>
/// <para>
/// <strong>Phase 2 — App-registered (ordered by <see cref="IInitializable.Order"/>):</strong>
/// All registered <see cref="IInitializable"/> services (including remote state) that
/// return <see langword="true"/> from <see cref="IInitializable.ShouldInitialize"/>.
/// </para>
/// </remarks>
internal sealed partial class InitializationOrchestrator(
	IEnumerable<IInitializable> initializables,
	IActivityState activityState,
	ClientUser clientUser,
	IServiceProvider serviceProvider,
	INotificationState notificationState,
	ILogger<InitializationOrchestrator> logger
) : IInitializationOrchestrator {

	private int _hasStarted;
	private int _hasCompleted;

	/// <inheritdoc />
	public bool HasStarted => this._hasStarted == 1;

	/// <inheritdoc />
	public bool HasCompleted => this._hasCompleted == 1;

	/// <inheritdoc />
	public void Start(CancellationToken cancellationToken = default) {
		if (Interlocked.Exchange(ref this._hasStarted, 1) == 1) {
			return;
		}

		_ = this.RunAsync(cancellationToken)
			.ContinueWith(
				t => Log.InitializationPipelineFailed(logger, t.Exception!.GetBaseException()),
				CancellationToken.None,
				TaskContinuationOptions.OnlyOnFaulted,
				TaskScheduler.Current);
	}

	private async Task RunAsync(CancellationToken cancellationToken) {
		try {

			// Resolve Phase 1 services only when authenticated — these services
			// depend on a valid identity. Skipped entirely for anonymous users.
			// Invariant: orchestrator only starts after IsAuthenticationComplete is true,
			// so clientUser.IsAuthenticated reflects the final settled auth state.
			IApplicationUserResolver? userResolver = null;
			IUserProfileEnricher? enricher = null;

			if (clientUser.IsAuthenticated) {
				userResolver = serviceProvider.GetService<IApplicationUserResolver>();
				enricher = serviceProvider.GetService<IUserProfileEnricher>();
			}

			var phase2Items = initializables
				.OrderBy(i => i.Order)
				.ToList();

			var totalTasks = phase2Items.Count
				+ (userResolver is not null ? 1 : 0)
				+ (enricher is not null ? 1 : 0);

			if (totalTasks == 0) {
				Interlocked.Exchange(ref this._hasCompleted, 1); // Set Completed = true
				activityState.ResetTasks(); // reset activity and ensure AppRouteView get notified
				Log.NoInitializationWork(logger);
				return;
			}

			// Replace the initial indeterminate task started by AppRouteView with
			// deterministic tracked work. The notification scope prevents an intermediate
			// UI update that would briefly make the application appear inactive.
			activityState.SetDisplayStatus("Initializing application...");
			using (activityState.CreateNotificationScope()) {
				activityState.ResetTasks();
				activityState.BeginTasks(totalTasks);
			}

			// Phase 1 — Cirreum-controlled: app user + profile enrichment
			if (userResolver is not null || enricher is not null) {
				await this.RunPhase1Async(userResolver, enricher, cancellationToken);
			}

			// Phase 2 — App-registered initializers
			if (phase2Items.Count > 0) {
				await this.RunPhase2Async(phase2Items, cancellationToken);
			}

		} catch (Exception ex) when (ex is not OperationCanceledException) {
			Log.InitializationPipelineFailed(logger, ex);

			activityState.LogError(
				sourceName: "Initialization Pipeline",
				exception: ex,
				displayMessage: "An unexpected initialization error occurred.");

			notificationState.AddNotification(Notification.Create(
				title: "Initialization Error",
				message: "An unexpected initialization error occurred.",
				type: NotificationType.Error));

		} finally {
			Interlocked.Exchange(ref this._hasCompleted, 1);
			// ResetTasks() notifies AppRouteView one final time with HasCompleted = true,
			// allowing the transition from Pending to Ready.
			activityState.ResetTasks();
		}
	}

	// -------------------------------------------------------------------------
	// Phase 1 — Cirreum-controlled
	// -------------------------------------------------------------------------

	private async Task RunPhase1Async(
		IApplicationUserResolver? userResolver,
		IUserProfileEnricher? enricher,
		CancellationToken cancellationToken) {

		// 1. Application user resolution
		if (userResolver is not null) {
			Log.LoadingApplicationUser(logger);
			activityState.SetDisplayStatus("Loading application user...");

			try {
				var applicationUser = await userResolver.ResolveAsync(clientUser.Id, cancellationToken);
				clientUser.SetAppUser(applicationUser);
				Log.ApplicationUserLoaded(logger, applicationUser is not null);
			} catch (Exception ex) when (ex is not OperationCanceledException) {
				Log.ApplicationUserLoadFailed(logger, ex);
				activityState.LogError(
					sourceName: "Application User",
					exception: ex,
					displayMessage: "The application user could not be loaded.");
				notificationState.AddNotification(Notification.Create(
					title: "Application User Error",
					message: "The application user could not be loaded.",
					type: NotificationType.Error));
				clientUser.SetAppUser(null);
			} finally {
				activityState.CompleteTask();
			}
		}

		// 2. Profile enrichment
		if (enricher is not null) {
			Log.EnrichingProfile(logger);
			activityState.SetDisplayStatus("Enriching user profile...");

			try {
				await enricher.EnrichProfileAsync(clientUser.Profile, clientUser.Identity);
				clientUser.SetEnrichmentCompleted();
				Log.ProfileEnrichmentComplete(logger);
			} catch (Exception ex) when (ex is not OperationCanceledException) {
				Log.ProfileEnrichmentFailed(logger, ex);
				activityState.LogError(
					sourceName: "Profile Enrichment",
					exception: ex,
					displayMessage: "The user profile could not be fully enriched.");
				notificationState.AddNotification(Notification.Create(
					title: "Profile Enrichment Error",
					message: "The user profile could not be fully enriched.",
					type: NotificationType.Error));
				// Always mark enrichment as complete to avoid blocking
				clientUser.SetEnrichmentCompleted();
			} finally {
				activityState.CompleteTask();
			}
		}
	}

	// -------------------------------------------------------------------------
	// Phase 2 — App-registered initializers
	// -------------------------------------------------------------------------

	private async Task RunPhase2Async(List<IInitializable> items, CancellationToken cancellationToken) {
		Log.BeginningPhase2(logger, items.Count);

		foreach (var item in items) {

			cancellationToken.ThrowIfCancellationRequested();

			// Evaluate late — Phase 1 may have mutated IUserState (e.g., SetAppUser)
			// which downstream initializables may depend on.
			if (!item.ShouldInitialize(clientUser)) {
				Log.SkippingService(logger, item.DisplayName);
				activityState.CompleteTask();
				continue;
			}

			var statusMessage = string.IsNullOrWhiteSpace(item.InitializationMessage)
				? $"Loading {item.DisplayName}..."
				: item.InitializationMessage;

			Log.InitializingService(logger, item.DisplayName);
			activityState.SetDisplayStatus(statusMessage);

			try {
				await item.InitializeAsync(activityState.SetDisplayStatus, cancellationToken);
				Log.ServiceInitialized(logger, item.DisplayName);
			} catch (Exception ex) when (ex is not OperationCanceledException) {
				Log.ServiceInitializationFailed(logger, item.DisplayName, ex);
				activityState.LogError(
					sourceName: item.DisplayName,
					exception: ex,
					displayMessage: $"{item.DisplayName} could not be initialized.");
				notificationState.AddNotification(Notification.Create(
					title: $"{item.DisplayName} Error",
					message: $"{item.DisplayName} could not be initialized.",
					type: NotificationType.Error));
				// Continue with other initializers
			} finally {
				activityState.CompleteTask();
			}
		}

		Log.Phase2Complete(logger, activityState.Errors.Count);
	}

	// -------------------------------------------------------------------------
	// Logging
	// -------------------------------------------------------------------------

	private static partial class Log {

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "No initialization work to perform")]
		public static partial void NoInitializationWork(ILogger logger);

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "Resolving application user via IApplicationUserResolver")]
		public static partial void LoadingApplicationUser(ILogger logger);

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "Application user load completed. Success: {IsSuccess}")]
		public static partial void ApplicationUserLoaded(ILogger logger, bool isSuccess);

		[LoggerMessage(Level = LogLevel.Error,
			Message = "Failed to load application user")]
		public static partial void ApplicationUserLoadFailed(ILogger logger, Exception exception);

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "Enriching user profile")]
		public static partial void EnrichingProfile(ILogger logger);

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "Profile enrichment complete")]
		public static partial void ProfileEnrichmentComplete(ILogger logger);

		[LoggerMessage(Level = LogLevel.Error,
			Message = "Profile enrichment failed")]
		public static partial void ProfileEnrichmentFailed(ILogger logger, Exception exception);

		[LoggerMessage(Level = LogLevel.Information,
			Message = "Beginning initialization for {ServiceCount} services")]
		public static partial void BeginningPhase2(ILogger logger, int serviceCount);

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "Skipping service (ShouldInitialize=false): {ServiceName}")]
		public static partial void SkippingService(ILogger logger, string serviceName);

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "Initializing service: {ServiceName}")]
		public static partial void InitializingService(ILogger logger, string serviceName);

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "Successfully initialized service: {ServiceName}")]
		public static partial void ServiceInitialized(ILogger logger, string serviceName);

		[LoggerMessage(Level = LogLevel.Error,
			Message = "Failed to initialize service: {ServiceName}")]
		public static partial void ServiceInitializationFailed(ILogger logger, string serviceName, Exception exception);

		[LoggerMessage(Level = LogLevel.Error,
			Message = "Initialization pipeline failed unexpectedly")]
		public static partial void InitializationPipelineFailed(ILogger logger, Exception exception);

		[LoggerMessage(Level = LogLevel.Information,
			Message = "Initialization complete. Errors: {ErrorCount}")]
		public static partial void Phase2Complete(ILogger logger, int errorCount);

	}

}