namespace Cirreum.Runtime.State;

using Cirreum.Runtime.Security;
using Cirreum.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestrates application initialization in two phases, coordinating Cirreum-controlled
/// services and app-registered <see cref="IInitializable"/> services while reporting
/// progress through <see cref="IInitializationState"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Phase 1 — Cirreum-controlled (fixed order):</strong>
/// Application user loading via <see cref="IApplicationUserFactory"/> and profile
/// enrichment via <see cref="IUserProfileEnricher"/>. These run first, in a fixed
/// order, only when the user is authenticated and the respective services are registered.
/// </para>
/// <para>
/// <strong>Phase 2 — App-registered (ordered by <see cref="IInitializable.Order"/>):</strong>
/// All registered <see cref="IInitializable"/> services (including data stores) that
/// return <see langword="true"/> from <see cref="IInitializable.ShouldInitialize"/>.
/// </para>
/// </remarks>
internal sealed partial class InitializationOrchestrator(
	IEnumerable<IInitializable> initializables,
	IInitializationState initState,
	ClientUser clientUser,
	IServiceProvider serviceProvider,
	ILogger<InitializationOrchestrator> logger
) : IInitializationOrchestrator {

	private bool _hasStarted;
	private bool _hasCompleted;

	/// <inheritdoc />
	public bool HasStarted => this._hasStarted;

	/// <inheritdoc />
	public bool HasCompleted => this._hasCompleted;

	/// <inheritdoc />
	public void Start() {
		if (this._hasStarted) {
			return;
		}

		this._hasStarted = true;

		// Resolve Phase 1 services once — avoids double DI resolution.
		IApplicationUserFactory? userFactory = null;
		IUserProfileEnricher? enricher = null;

		if (clientUser.IsAuthenticated) {
			userFactory = serviceProvider.GetService<IApplicationUserFactory>();
			enricher = serviceProvider.GetService<IUserProfileEnricher>();
		}

		var phase2Items = initializables
			.OrderBy(i => i.Order)
			.ToList();

		if (userFactory is null && enricher is null && phase2Items.Count == 0) {
			this._hasCompleted = true;
			Log.NoInitializationWork(logger);
			return;
		}

		// Calculate total tasks upfront for deterministic progress tracking.
		// Phase 2 items that are later skipped via ShouldInitialize still count
		// toward the total — they're "resolved" instantly.
		var totalTasks = phase2Items.Count
			+ (userFactory is not null ? 1 : 0)
			+ (enricher is not null ? 1 : 0);

		// Synchronously set total before returning — prevents any
		// rendering gap where the app could briefly appear ready.
		initState.SetTotalTasks(totalTasks);

		_ = this.RunAsync(userFactory, enricher, phase2Items);
	}

	private async Task RunAsync(
		IApplicationUserFactory? userFactory,
		IUserProfileEnricher? enricher,
		List<IInitializable> phase2Items) {
		// Phase 1 — Cirreum-controlled: app user + profile enrichment
		if (userFactory is not null || enricher is not null) {
			await this.RunPhase1Async(userFactory, enricher);
		}

		// Phase 2 — App-registered initializers
		if (phase2Items.Count > 0) {
			await this.RunPhase2Async(phase2Items);
		}

		this._hasCompleted = true;
	}

	// -------------------------------------------------------------------------
	// Phase 1 — Cirreum-controlled
	// -------------------------------------------------------------------------

	private async Task RunPhase1Async(
		IApplicationUserFactory? userFactory,
		IUserProfileEnricher? enricher) {

		// 1. Application user loading
		if (userFactory is not null) {
			Log.LoadingApplicationUser(logger);
			initState.SetDisplayStatus("Loading user profile...");

			try {
				var result = await userFactory.CreateUserAsync(clientUser);
				clientUser.SetAppUser(result.IsSuccess ? result.Value : null);
				Log.ApplicationUserLoaded(logger, result.IsSuccess);
			} catch (Exception ex) {
				Log.ApplicationUserLoadFailed(logger, ex);
				initState.LogError("Application User", ex);
				clientUser.SetAppUser(null);
			} finally {
				initState.CompleteTask();
			}
		}

		// 2. Profile enrichment
		if (enricher is not null) {
			Log.EnrichingProfile(logger);
			initState.SetDisplayStatus("Loading user profile...");

			try {
				await enricher.EnrichProfileAsync(clientUser.Profile, clientUser.Identity);
				clientUser.SetEnrichmentCompleted();
				Log.ProfileEnrichmentComplete(logger);
			} catch (Exception ex) {
				Log.ProfileEnrichmentFailed(logger, ex);
				initState.LogError("Profile Enrichment", ex);
				// Always mark enrichment as complete to avoid blocking
				clientUser.SetEnrichmentCompleted();
			} finally {
				initState.CompleteTask();
			}
		}

	}

	// -------------------------------------------------------------------------
	// Phase 2 — App-registered initializers
	// -------------------------------------------------------------------------

	private async Task RunPhase2Async(List<IInitializable> items) {
		Log.BeginningPhase2(logger, items.Count);

		foreach (var item in items) {
			// Evaluate late — Phase 1 may have mutated IUserState (e.g., SetAppUser)
			// which downstream initializables may depend on.
			if (!item.ShouldInitialize(clientUser)) {
				Log.SkippingService(logger, item.DisplayName);
				initState.CompleteTask();
				continue;
			}

			var statusMessage = item.InitializationMessage
				?? $"Loading {item.DisplayName}...";

			Log.InitializingService(logger, item.DisplayName);
			initState.SetDisplayStatus(statusMessage);

			try {
				await item.InitializeAsync(initState.SetDisplayStatus);
				Log.ServiceInitialized(logger, item.DisplayName);
			} catch (Exception ex) {
				Log.ServiceInitializationFailed(logger, item.DisplayName, ex);
				initState.LogError(item.DisplayName, ex);
				// Continue with other initializers
			} finally {
				initState.CompleteTask();
			}
		}

		Log.Phase2Complete(logger, initState.ErrorCount);
	}

	// -------------------------------------------------------------------------
	// Logging
	// -------------------------------------------------------------------------

	private static partial class Log {

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "No initialization work to perform")]
		public static partial void NoInitializationWork(ILogger logger);

		[LoggerMessage(Level = LogLevel.Debug,
			Message = "Loading application user via IApplicationUserFactory")]
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

		[LoggerMessage(Level = LogLevel.Information,
			Message = "Initialization complete. Errors: {ErrorCount}")]
		public static partial void Phase2Complete(ILogger logger, int errorCount);

	}

}
