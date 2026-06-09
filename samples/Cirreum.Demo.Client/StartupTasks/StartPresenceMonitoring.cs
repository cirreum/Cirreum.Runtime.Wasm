namespace Cirreum.Demo.Client.StartupTasks;

using System.Threading.Tasks;

internal class StartPresenceMonitoring(
	IUserPresenceMonitor monitor) : IStartupTask {

	public int Order => int.MaxValue;

	public async ValueTask ExecuteAsync() {
		if (DynamicAuthInterop.GetAuthLibraryType() == AuthenticationLibraryType.MSAL) {
			await monitor.StartMonitoringPresence();
		}
	}

}