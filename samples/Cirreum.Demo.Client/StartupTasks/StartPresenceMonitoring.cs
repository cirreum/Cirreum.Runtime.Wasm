namespace Cirreum.Demo.Client.StartupTasks;

using System.Threading.Tasks;

internal class StartPresenceMonitoring(
	IUserPresenceMonitor monitor,
	IUserState userState) : IStartupTask {

	public int Order => int.MaxValue;

	public async ValueTask ExecuteAsync() {
		if (userState.AuthenticationType == AuthenticationLibraryType.MSAL) {
			await monitor.StartMonitoringPresence();
		}
	}

}