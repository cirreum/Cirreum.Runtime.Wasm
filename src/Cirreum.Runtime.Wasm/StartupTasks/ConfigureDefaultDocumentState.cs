namespace Cirreum.Runtime.StartupTasks;

using System.Threading.Tasks;

sealed class ConfigureDefaultDocumentState(
	IBrowserDocumentState documentState,
	IJSAppModule jsApp
) : IStartupTask {

	int IStartupTask.Order => int.MinValue;

	ValueTask IStartupTask.ExecuteAsync() {

		using var _ = documentState.CreateNotificationScope();

		documentState.SetIsStandAlone(jsApp.IsStandAlone());

		documentState.SetAppName(jsApp.GetAppName());

		documentState.SetDocumentTitleSeparator("|");
		documentState.SetDocumentTitlePrefix("");
		documentState.SetDocumentTitleSuffix($"{documentState.AppName}");

		return ValueTask.CompletedTask;

	}

}