namespace Cirreum.Runtime;

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

sealed class DomainEnvironment(
	IWebAssemblyHostEnvironment hostEnvironment,
	IJSAppModule module
) : IDomainEnvironment {
	private string? _appName;
	public string ApplicationName => _appName ??= module.GetAppName();
	public string EnvironmentName { get; } = hostEnvironment.Environment;
	public DomainRuntimeType RuntimeType { get; } = DomainRuntimeType.BlazorWasm;
}