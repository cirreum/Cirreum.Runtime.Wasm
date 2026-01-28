namespace Cirreum.Runtime.StartupTasks;

using System.Threading.Tasks;

sealed class ConfigurePageState(
	IPageState pageState,
	IJSAppModule jsApp
) : IStartupTask {

	int IStartupTask.Order => int.MinValue;

	ValueTask IStartupTask.ExecuteAsync() {

		using var _ = pageState.CreateNotificationScope();

		pageState.SetIsStandAlone(jsApp.IsStandAlone());

		pageState.SetAppName(jsApp.GetAppName());

		pageState.SetPageTitleSeparator("|");
		pageState.SetPageTitlePrefix("");
		pageState.SetPageTitleSuffix($"{pageState.AppName}");

		return ValueTask.CompletedTask;

	}

}