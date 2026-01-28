namespace Cirreum.Runtime.Authentication.PostProcessors;

using Cirreum;
using Cirreum.Runtime.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Foundation implementation that auto-loads application users after authentication.
/// </summary>
sealed class ApplicationUserProcessor<T>(
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
			logger.LogError("Missing service provider.");
			return;
		}

		// User locator pattern, to prevent lifetime errors
		var loader = serviceProvider.GetService<IApplicationUserLoader<T>>();
		if (loader is null) {
			logger.LogWarning("No application user loader is available.");
			return;
		}

		if (userState is not ClientUser clientUser) {
			logger.LogError("Invalid user state instance type, expecting 'ClientUser'.");
			return;
		}

		if (clientUser.IsApplicationUserLoaded) {
			logger.LogWarning("Application User has already been loaded.");
			return;
		}

		if (!clientUser.IsAuthenticated) {
			logger.LogWarning("User is not authenticated.");
			return;
		}

		var result = await loader.TryLoadUserAsync(serviceProvider, clientUser.Id, cancellationToken);

		if (result.IsSuccess) {
			clientUser.SetAppUser(result.Value);
			var userType = result.Value.GetType().Name;
			if (logger.IsEnabled(LogLevel.Information)) {
				logger.LogInformation("Application User loaded of Type {UserType}.", userType);
			}
			return;
		}

		// Set to null to indicate that loading was attempted but no user was found/loadable.
		// This sets IsApplicationUserLoaded = true with ApplicationUser = null.
		// Using ClearApplicationUser() would reset IsApplicationUserLoaded = false, indicating
		// no load attempt was made.
		clientUser.SetAppUser(null);

		if (logger.IsEnabled(LogLevel.Error)) {
			logger.LogError(result.Error, "Error encountered loading Application User of Type {UserType}.", typeof(T).Name);
		}

	}

}