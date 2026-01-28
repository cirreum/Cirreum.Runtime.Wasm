namespace Cirreum.Demo.Client.StartupTasks;

public class ConfigureContentSecurityPolicy(ICspBuilder builder) : IStartupTask {

	public int Order => 0;

	public ValueTask ExecuteAsync() {

		//
		// Connect Sources
		//
		builder

			// App Backend
			.AddConnectSource("https://core-demo-api-container.wittysky-81847266.westus3.azurecontainerapps.io")

			// Auth/Identity Providers
			.AddConnectSource("https://graph.microsoft.com")
			.AddConnectSource("https://login.microsoftonline.com")
			.AddConnectSource("https://correxternaldev.ciamlogin.com")
			.AddConnectSource("https://lh3.googleusercontent.com")
			.AddConnectSource("https://people.googleapis.com")
			.AddConnectSource("https://api.descope.com")
			.AddConnectSource("https://dev-yn1oshrbo32te5o0.us.auth0.com")

			// Azure Monitor/AppInsights
			.AddConnectSource("https://westus3-1.in.applicationinsights.azure.com")
			.AddConnectSource("https://js.monitor.azure.com")
			.AddConnectSource("https://dc.services.visualstudio.com")
			.AddConnectSource("https://dc.applicationinsights.azure.com")
			.AddConnectSource("https://dc.applicationinsights.microsoft.com")
			.AddConnectSource("https://live.applicationinsights.azure.com")
			.AddConnectSource("https://rt.applicationinsights.microsoft.com")
			.AddConnectSource("https://rt.services.visualstudio.com")
			.AddConnectSource("https://profiler.monitor.azure.com")
			.AddConnectSource("https://snapshot.monitor.azure.com")

			// Fake Data Provider
			.AddConnectSource("https://api.json-generator.com");


		//
		// Script Sources
		//
		builder
			.AddWasmUnsafeEvalScript()
			.AddScriptSource("https://unpkg.com");


		//
		// Style Sources
		//
		builder
			.AddUnsafeInlineStyle()
			.AddStyleSource("https://unpkg.com")
			.AddStyleSource("https://fonts.cdnfonts.com");

		//
		// Font Sources
		//
		builder
			.AddFontSource("https://unpkg.com")
			.AddFontSource("https://fonts.cdnfonts.com");

		//
		// General
		//
		builder
			.AddSource("frame-src", "https://correxternaldev.ciamlogin.com")
			.AddSource("frame-src", "https://api.descope.com")
			.AddSource("frame-src", "https://dev-yn1oshrbo32te5o0.us.auth0.com");

#if DEBUG
		builder
			.AddConnectSource("ws://localhost:*")
			.AddConnectSource("wss://localhost:*")
			.AddConnectSource("http://localhost:*")
			.AddConnectSource("https://localhost:*")
			.AddConnectSource("https://raw.githubusercontent.com");
#endif

		return ValueTask.CompletedTask;

	}

}