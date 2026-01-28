//
// NOTE: we are NOT using an 'appsettings.json' file.
//
// Even though our config settings can be found via
// dis-assembly, this is only meant to prevent it from
// being super easy and convenient to download on demand.
//


// ******************************************************************************
// Configure the WebAssemblyApplication
//
var builder = DomainApplication
	.CreateBuilder(args);

// ******************************************************************************
// Configure Logging Level
//
builder.Logging.SetMinimumLevel(builder.HostEnvironment.IsProduction() ? LogLevel.Information : LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.RenderTree.Renderer", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.Sections", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.Authorization", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.Routing", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.WebAssembly.Authentication", LogLevel.Information);


// ******************************************************************************
// Configure the WebAssemblyHostBuilder
//
builder.RootComponents.Add<HeadOutlet>(App.HeadOutlet);
builder.RootComponents.Add<App>(App.ElementId);


// ******************************************************************************
// Add Clients for your Remote Services
//

// Authorization modes:
// - AuthorizationHeader = null (default)           → OIDC/OAuth via AuthorizationMessageHandler
// - AuthorizationHeader = { Scheme, Value }        → Pre-shared token (e.g., Bearer)
// - AuthorizationHeader = AuthorizationHeaderSettings.None → No auth (public API)

// Entra External
//	ServiceScopes = ["api://5a886588-c56f-4d9c-a656-6cb47ee3e8bd/access_as_user"]
// Entra Workforce
//	ServiceScopes = ["api://eecfb243-5bbe-49a4-9f1b-c33cb4c6e21e/access_as_user"]

// Auth Ping Api - Must match AddAuth (Entra or EntraExternal)
//var primaryApi = new RemoteServiceOptions {
//	ServiceUri = new("https://localhost:7040/"),
//	ServiceScopes = ["api://5a886588-c56f-4d9c-a656-6cb47ee3e8bd/access_as_user"]
//};
//builder.AddRemoteClient<PingAuthClient>(primaryApi);
// Any other clients that use the primary Api
//builder.AddRemoteClient<CustomerClient>(primaryApi);
//builder.AddRemoteClient<TasksClient>(primaryApi);

// No-Auth Ping Api
// We provided a specific client name instead of using the default TClient type name
//builder.AddRemoteClient<PingClient>(o => {
//	o.ServiceUri = new("https://localhost:7040/");
//	// o.AuthorizationHeader                                  // default/unset: uses OIDC/OAuth
//	o.AuthorizationHeader = AuthorizationHeaderSettings.None; // explicit: no auth (public/anonymous API)
//}, "PingApiNoAuth");

// Data Generator Json Api
// use predetermined api access token
//https://api.json-generator.com/templates/FXImuy6I7yBj/data
builder.AddRemoteClient("FakeData", o => {
	o.ApplicationName = ""; // don't send to public api
	o.ServiceUri = new("https://api.json-generator.com/templates/");
	o.AuthorizationHeader = new AuthorizationHeaderSettings {
		Scheme = "bearer",
		Value = "5h70ul87b4czgcpaqwnpp2ii7akd8b6gfa82uf0d"
	};
});

builder.Services.AddScoped(sp => new HttpClient {
	BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// ******************************************************************************
// Add Authentication/Authorization
//
// *NOTE*
// Be sure and update index.html appropriately, and ensure the Idp's URL(s) are
// included in the CSP (see StartupTasks 'ConfigureContentSecurityPolicy'
//

//
// MSAL (Entra)
//
//builder
//	.AddEntraAuth(
//		"08c2d181-67c6-4a2e-95da-7a0a91926ecd",
//		"3cf0d4f6-b342-42f3-865d-4f7ce7fade4a")
//	.AddSessionMonitoring()
//	.AddGraphServices()
//		.WithMinimalGraphEnrichment()
//		.WithGraphUserPresence(60000); // optional UserPresence monitor

//
// MSAL (Entra External)
//
//builder
//	.AddEntraExternalAuth(
//		"correxternaldev",
//		"65df106e-85b4-42db-9b31-97b80fd4bcda")
//	.AddClaimsExtender<CustomRoleClaimsExtender>()
//	.AddApplicationUser<SampleAppUser, SampleAppUserLoader>()
//	.AddSessionMonitoring()
//	.AddGraphServices()
//		.WithGraphEnrichment();

//
// OIDC
//
//builder.AddMyOidcAuth(); //local helper extension method


// ******************************************************************************
// Add Application State
//
builder.AddClientState(state => state
	.RegisterState<INavMenuState, NavMenuState>()
	.RegisterState<ITabRenderModeState, TabRenderModeState>()
	.RegisterState<INavMenuState, NavMenuState>()
	.RegisterState<INotificationState, NotificationState>()
	.RegisterEncryptor(BuiltInEncryption.Base64Obfuscation)
	.AddDataStores()
		.WithAutoInitialization()
// register previously used algorithms (None and Base64 are built-in)
//.RegisterDecryptor(BuiltInEncryption.XorObfuscation(42))
//.RegisterDecryptor(BuiltInEncryption.XorObfuscation(123))
//.RegisterDecryptor(new MyCustomEncryptor())
);


// ******************************************************************************
// Add Application Services
//
//builder.Services.Add...

// Application ViewModels
builder.Services.AddScoped<UserSessionViewModel>();
builder.Services.AddScoped<IMermaidService, MermaidService>();


// ******************************************************************************
// Add AppInsights
//
//var appinsights = "InstrumentationKey=a1e3d2fc-1610-4c8c-a557-34500df7e32b;IngestionEndpoint=https://westus3-1.in.applicationinsights.azure.com/;ApplicationId=a5e09368-609e-479b-bc45-f389ea2a240f";
//builder
//	.AddAppInsights(appinsights, correlatedServices: ["localhost"]);


// ******************************************************************************
// Build and Run the app
//
await builder.BuildAndRunAsync();
