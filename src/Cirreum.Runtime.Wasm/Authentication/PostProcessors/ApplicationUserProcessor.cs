namespace Cirreum.Runtime.Authentication.PostProcessors;

using Cirreum;
using Cirreum.Runtime.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

/// <summary>
/// Foundation implementation that auto-loads application users after authentication.
/// </summary>
/// <remarks>
/// <para>
/// Runs as part of Phase 2 (deferred, best-effort) in
/// <see cref="CommonClaimsPrincipalFactory{TAccount}"/> after the
/// <see cref="ClaimsPrincipal"/> has been fully constructed and returned to Blazor.
/// </para>
/// <para>
/// Resolves an <see cref="IApplicationUserLoader{T}"/> from the service provider
/// and calls <see cref="IApplicationUserLoader{T}.TryLoadUserAsync"/> to load the
/// domain user associated with the authenticated identity. On success, the loaded
/// user is set on the <see cref="ClientUser"/> instance. On failure, the user is
/// explicitly set to <see langword="null"/> to indicate that a load attempt was made
/// but no user was found — distinguishable from no attempt having been made via
/// <see cref="IUserState.IsApplicationUserLoaded"/>.
/// </para>
/// </remarks>
sealed partial class ApplicationUserProcessor<T>(
	ILogger<ApplicationUserProcessor<T>> logger
) : IAuthenticationPostProcessor
	where T : class, IApplicationUser {

	public int Order { get; } = 100;

	/// <inheritdoc/>
	public async Task ProcessAsync(
		IServiceProvider serviceProvider,
		IUserState userState,
		CancellationToken cancellationToken = default) {

		if (serviceProvider is null) {
			Log.MissingServiceProvider(logger);
			return;
		}

		// User locator pattern — prevents lifetime errors from direct injection
		var loader = serviceProvider.GetService<IApplicationUserLoader<T>>();
		if (loader is null) {
			Log.NoLoader(logger, typeof(T).Name);
			return;
		}

		if (userState is not ClientUser clientUser) {
			Log.InvalidUserStateType(logger, userState?.GetType().Name ?? "null");
			return;
		}

		if (clientUser.IsApplicationUserLoaded) {
			Log.AlreadyLoaded(logger);
			return;
		}

		if (!clientUser.IsAuthenticated) {
			Log.NotAuthenticated(logger);
			return;
		}

		var result = await loader.TryLoadUserAsync(serviceProvider, clientUser.Id, cancellationToken);

		if (result.IsSuccess) {
			clientUser.SetAppUser(result.Value);
			Log.UserLoaded(logger, result.Value.GetType().Name);
			return;
		}

		// Set to null to indicate that loading was attempted but no user was found/loadable.
		// This sets IsApplicationUserLoaded = true with ApplicationUser = null.
		// Using ClearApplicationUser() would reset IsApplicationUserLoaded = false, indicating
		// no load attempt was made.
		clientUser.SetAppUser(null);
		Log.UserLoadFailed(logger, result.Error, typeof(T).Name);
	}

	// -------------------------------------------------------------------------
	// Logging
	// -------------------------------------------------------------------------

	private static partial class Log {
		[LoggerMessage(Level = LogLevel.Error, Message = "Missing service provider.")]
		internal static partial void MissingServiceProvider(ILogger logger);

		[LoggerMessage(Level = LogLevel.Warning, Message = "No application user loader is available for type {UserType}.")]
		internal static partial void NoLoader(ILogger logger, string userType);

		[LoggerMessage(Level = LogLevel.Error, Message = "Invalid user state instance type '{ActualType}', expected 'ClientUser'.")]
		internal static partial void InvalidUserStateType(ILogger logger, string actualType);

		[LoggerMessage(Level = LogLevel.Warning, Message = "Application user has already been loaded.")]
		internal static partial void AlreadyLoaded(ILogger logger);

		[LoggerMessage(Level = LogLevel.Warning, Message = "User is not authenticated.")]
		internal static partial void NotAuthenticated(ILogger logger);

		[LoggerMessage(Level = LogLevel.Information, Message = "Application user loaded of type {UserType}.")]
		internal static partial void UserLoaded(ILogger logger, string userType);

		[LoggerMessage(Level = LogLevel.Error, Message = "Error loading application user of type {UserType}.")]
		internal static partial void UserLoadFailed(ILogger logger, Exception? error, string userType);
	}

}