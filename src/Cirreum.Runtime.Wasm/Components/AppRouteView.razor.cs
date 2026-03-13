namespace Cirreum.Components;

using Cirreum.Security;
using Cirreum.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

/// <summary>
/// Application-level route view that enforces authentication, initialization, and
/// application user readiness before rendering routed page components. Drop-in
/// replacement for <see cref="RouteView"/> or <see cref="AuthorizeRouteView"/>
/// inside a <see cref="Router"/>'s <c>&lt;Found&gt;</c> block.
/// </summary>
/// <remarks>
/// <para>
/// Renders <see cref="RouteView"/> for authentication callback paths and pending
/// states, and <see cref="AuthorizeRouteView"/> once the application is ready.
/// Page components are never instantiated until readiness is confirmed.
/// </para>
/// <para>
/// The state machine is re-evaluated on every <see cref="IUserState"/> change,
/// <see cref="IInitializationState"/> change, and <see cref="RouteData"/> update.
/// States are evaluated top-to-bottom; first match wins:
/// </para>
/// <list type="number">
///   <item><description>Authentication path → <see cref="RouteView"/> with <see cref="PendingLayout"/></description></item>
///   <item><description>Route requires auth + not authenticated → <see cref="RedirectToLogin"/> (no layout, no DOM)</description></item>
///   <item><description>Initialization in progress → <see cref="PendingLayout"/> (covers user loading, enrichment, and any registered remote states)</description></item>
///   <item><description>App user not found → <see cref="NotProvisioned"/> (only when <see cref="IApplicationUserFactory"/> is registered)</description></item>
///   <item><description>App user disabled → <see cref="Disabled"/> (only when <see cref="IApplicationUserFactory"/> is registered)</description></item>
///   <item><description>All checks pass + auth registered → <see cref="AuthorizeRouteView"/> with <see cref="DefaultLayout"/></description></item>
///   <item><description>All checks pass + no auth registered → <see cref="RouteView"/> with <see cref="DefaultLayout"/></description></item>
/// </list>
/// </remarks>
public sealed partial class AppRouteView : ComponentBase, IDisposable {

	[Inject] private IUserState UserState { get; set; } = default!;
	[Inject] private IStateManager StateManager { get; set; } = default!;
	[Inject] private NavigationManager Navigation { get; set; } = default!;
	[Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
	[Inject] private IInitializationState InitializationState { get; set; } = default!;
	[Inject] private IInitializationOrchestrator InitOrchestrator { get; set; } = default!;

	// -------------------------------------------------------------------------
	// Parameters
	// -------------------------------------------------------------------------

	/// <summary>
	/// The route data resolved by the <see cref="Router"/>. Pass the <c>routeData</c>
	/// context variable from the <c>&lt;Found&gt;</c> block.
	/// </summary>
	[Parameter, EditorRequired]
	public RouteData RouteData { get; set; } = default!;

	/// <summary>
	/// The layout rendered for authenticated, authorized routes — typically the
	/// application's main navigation layout.
	/// </summary>
	[Parameter, EditorRequired]
	public Type DefaultLayout { get; set; } = default!;

	/// <summary>
	/// The layout rendered during authentication and initialization pending states.
	/// Defaults to <see cref="DefaultLayout"/> when not specified. The splash
	/// screen typically lives in this layout and self-manages via
	/// <see cref="IInitializationState"/>.
	/// </summary>
	[Parameter]
	public Type? PendingLayout { get; set; }

	/// <summary>
	/// The base-relative path prefix used for OIDC/MSAL authentication callbacks.
	/// Defaults to <c>"authentication"</c>, matching Blazor's standard
	/// <c>/authentication/{action}</c> route convention.
	/// </summary>
	[Parameter]
	public string AuthenticationBasePath { get; set; } = "authentication";

	/// <summary>
	/// Rendered when the application user factory completed but returned <see langword="null"/> —
	/// the identity is authenticated but has no account in this application.
	/// </summary>
	/// <remarks>
	/// Only evaluated when an <see cref="IApplicationUserFactory"/> is registered.
	/// When no factory is registered, transitions directly from initialized to ready.
	/// </remarks>
	[Parameter]
	public RenderFragment? NotProvisioned { get; set; }

	/// <summary>
	/// Rendered when the application user exists but <see cref="IApplicationUser.IsEnabled"/>
	/// is <see langword="false"/>. The user is treated as unable to access the application.
	/// </summary>
	[Parameter]
	public RenderFragment? Disabled { get; set; }

	/// <summary>
	/// Rendered inside <see cref="AuthorizeRouteView"/>'s <c>NotAuthorized</c> section when
	/// an authenticated user lacks the required authorization policy for a route.
	/// Defaults to <see cref="RedirectToLogin"/> when not specified.
	/// </summary>
	[Parameter]
	public RenderFragment? NotAuthorizedContent { get; set; }

	// -------------------------------------------------------------------------
	// RedirectToLogin pass-through parameters
	// -------------------------------------------------------------------------

	/// <summary>
	/// The path to the login page used by the built-in <see cref="RedirectToLogin"/> instances.
	/// Defaults to <c>"authentication/login"</c>.
	/// </summary>
	[Parameter]
	public string? LoginPath { get; set; } = "authentication/login";

	/// <summary>
	/// Optional login hint passed to the identity provider to pre-populate the username field.
	/// Forwarded to the built-in <see cref="RedirectToLogin"/> instances.
	/// </summary>
	[Parameter]
	public string? LoginHint { get; set; }

	/// <summary>
	/// Optional OIDC prompt parameter to control IdP interaction behavior.
	/// Forwarded to the built-in <see cref="RedirectToLogin"/> instances.
	/// </summary>
	/// <remarks>
	/// Use <c>OidcPrompt.Login</c> to force re-authentication even when an active session exists.
	/// Use <c>OidcPrompt.None</c> for silent checks.
	/// </remarks>
	[Parameter]
	public OidcPrompt? LoginPrompt { get; set; }

	/// <summary>
	/// Optional domain hint passed to the identity provider to skip the organization/account
	/// picker and route directly to the specified tenant's login page.
	/// Maps to the OIDC <c>domain_hint</c> parameter.
	/// </summary>
	[Parameter]
	public string? DomainHint { get; set; }

	// -------------------------------------------------------------------------
	// State
	// -------------------------------------------------------------------------

	private IDisposable? _userStateSubscription;
	private IDisposable? _initStateSubscription;
	private ViewState _viewState;
	private bool _requiresApplicationUser;
	private bool _useAuthorizedRouting;

	private Type ResolvedPendingLayout => this.PendingLayout ?? this.DefaultLayout;

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	private string? _redirectReturnUrl;

	public override async Task SetParametersAsync(ParameterView parameters) {
		this._redirectReturnUrl = this.Navigation.Uri;
		await base.SetParametersAsync(parameters);
		if (this.EvaluateState()) {
			await this.InvokeAsync(this.StateHasChanged);
		}
	}

	protected override void OnInitialized() {
		this._requiresApplicationUser = this.ServiceProvider.GetService<IApplicationUserFactory>() is not null;
		this._useAuthorizedRouting = this.ServiceProvider.GetService<AuthenticationStateProvider>() is not null;
		this._userStateSubscription = this.StateManager.Subscribe<IUserState>(this.OnUserStateChanged);
		this._initStateSubscription = this.StateManager.Subscribe<IInitializationState>(this.OnInitStateChanged);
		this.EvaluateState();
	}

	// -------------------------------------------------------------------------
	// Event Handlers
	// -------------------------------------------------------------------------

	private void OnUserStateChanged(IUserState _) {
		if (this.EvaluateState()) {
			this.InvokeAsync(this.StateHasChanged);
		}
	}

	private void OnInitStateChanged(IInitializationState _) {
		if (this.EvaluateState()) {
			this.InvokeAsync(this.StateHasChanged);
		}
	}

	// -------------------------------------------------------------------------
	// State Evaluation
	// -------------------------------------------------------------------------

	/// <summary>
	/// Walks the state table top-to-bottom and returns <see langword="true"/>
	/// when the view state actually changed.
	/// </summary>
	private bool EvaluateState() {
		var next = this.ComputeViewState();
		if (next == this._viewState) {
			return false;
		}
		this._viewState = next;
		return true;
	}

	private ViewState ComputeViewState() {

		// 1. Authentication callback path — route normally with pending layout
		var relativePath = this.Navigation.ToBaseRelativePath(this.Navigation.Uri);
		if (relativePath.StartsWith(this.AuthenticationBasePath, StringComparison.OrdinalIgnoreCase)) {
			return ViewState.AuthenticationPath;
		}

		// 2. Route requires auth + not authenticated — redirect to login, no layout or DOM rendered
		if (RouteRequiresAuthorization(this.RouteData.PageType) && !this.UserState.IsAuthenticated) {
			return ViewState.RedirectToLogin;
		}

		// 3. Trigger initialization if not yet started — synchronous flip to IsInitializing
		if (!this.InitOrchestrator.HasStarted) {
			this.InitOrchestrator.Start();
		}

		// 4. Initialization in progress — splash covers auth, user loading, enrichment, and remote state
		if (this.InitializationState.IsInitializing) {
			return ViewState.Pending;
		}

		// 5–6. Application user checks (only when a factory is registered)
		if (this._requiresApplicationUser) {

			// 5. Factory completed but no account exists in this application
			if (this.UserState.ApplicationUser is null) {
				return ViewState.NotProvisioned;
			}

			// 6. Account exists but is disabled
			if (!this.UserState.ApplicationUser.IsEnabled) {
				return ViewState.Disabled;
			}
		}

		// 7. All checks pass
		return ViewState.Ready;

	}

	/// <summary>
	/// Returns <see langword="true"/> when the page type has an <see cref="AuthorizeAttribute"/>
	/// and does not have an <see cref="AllowAnonymousAttribute"/>.
	/// </summary>
	private static bool RouteRequiresAuthorization(Type pageType) {
		if (pageType.GetCustomAttributes<AllowAnonymousAttribute>(true).Any()) {
			return false;
		}
		return pageType.GetCustomAttributes<AuthorizeAttribute>(true).Any();
	}

	// -------------------------------------------------------------------------
	// Disposal
	// -------------------------------------------------------------------------

	public void Dispose() {
		this._userStateSubscription?.Dispose();
		this._initStateSubscription?.Dispose();
	}

	// -------------------------------------------------------------------------
	// Types
	// -------------------------------------------------------------------------

	private enum ViewState {
		Uninitialized,
		AuthenticationPath,
		RedirectToLogin,
		Pending,
		NotProvisioned,
		Disabled,
		Ready
	}

}