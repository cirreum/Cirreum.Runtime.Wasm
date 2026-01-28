namespace Cirreum.Runtime.StartupTasks;

using Cirreum.Runtime.Authentication;
using Cirreum.Runtime.Security;

sealed class ConfigureUserState(
	IUserState user
) : IStartupTask {
	int IStartupTask.Order => int.MinValue;

	ValueTask IStartupTask.ExecuteAsync() {
		if (user is ClientUser currentUser) {
			var libraryType = DynamicAuthInterop.GetAuthLibraryType();
			if (libraryType.HasValue) {
				currentUser.SetAuthenticationLibrary(libraryType.Value);
			}
		}
		return ValueTask.CompletedTask;
	}
}