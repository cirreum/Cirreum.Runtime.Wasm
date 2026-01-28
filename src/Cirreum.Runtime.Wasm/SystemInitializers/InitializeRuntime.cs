namespace Cirreum.Runtime.SystemInitializers;

using Microsoft.Extensions.DependencyInjection;

sealed class InitializeRuntime : ISystemInitializer {
	public async ValueTask RunAsync(IServiceProvider serviceProvider) {

		var appModule = serviceProvider.GetRequiredService<IJSAppModule>();
		await appModule.InitializeAsync();

		// DomainContext depends on DomainEnvironment
		// And DomainEnvironment depends on IJSAppModule
		var initializer = serviceProvider.GetRequiredService<IDomainContextInitializer>();
		initializer.Initialize();

	}
}