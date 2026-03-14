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
/// states. Once the application is ready, renders either <see cref="AuthorizeRouteView"/>
/// or <see cref="RouteView"/> depending on whether authentication services are registered.
/// Page components are never instantiated until readiness is confirmed.
/// </para>
/// <para>
/// The state machine is re-evaluated on every <see cref="IUserState"/> change,
/// <see cref="IActivityState"/> change, and <see cref="RouteData"/> update.
/// States are evaluated top-to-bottom; first match wins:
/// </para>
/// <list type="number">
///   <item><description>Authentication path → <see cref="RouteView"/> with <see cref="PendingLayout"/></description></item>
///   <item><description>Route requires auth + not authenticated → <see cref="RedirectToLogin"/> (no layout, no DOM)</description></item>
///   <item><description>Initialization in progress → <see cref="PendingLayout"/> (covers application user loading, profile enrichment, and any registered initializable services, including remote state)</description></item>
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
	[Inject] private IActivityState ActivityState { get; set; } = default!;
	[Inject] private IInitializationOrchestrator Orchestrator { get; set; } = default!;

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
	/// <see cref="IActivityState"/>.
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

	private int _renderQueued;
	private IDisposable? _userStateSubscription;
	private IDisposable? _activityStateSubscription;
	private ViewState _viewState = ViewState.Pending;
	private bool _requiresApplicationUser;
	private bool _hasAuthenticationRouting;
	private string? _redirectReturnUrl;

	private Type ResolvedPendingLayout => this.PendingLayout ?? this.DefaultLayout;

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	public override async Task SetParametersAsync(ParameterView parameters) {
		this._redirectReturnUrl = this.Navigation.Uri;
		await base.SetParametersAsync(parameters);

		this.EnsureInitializationStarted();
		this._viewState = this.ComputeViewState();
	}

	protected override void OnInitialized() {
		this._requiresApplicationUser = this.ServiceProvider.GetService<IApplicationUserFactory>() is not null;
		this._hasAuthenticationRouting = this.ServiceProvider.GetService<AuthenticationStateProvider>() is not null;
		this._userStateSubscription = this.StateManager.Subscribe<IUserState>(this.OnUserStateChanged);
		this._activityStateSubscription = this.StateManager.Subscribe<IActivityState>(this.OnActivityStateChanged);
	}

	// -------------------------------------------------------------------------
	// Event Handlers
	// -------------------------------------------------------------------------

	private void OnUserStateChanged(IUserState _) => this.RequestRender();

	private void OnActivityStateChanged(IActivityState _) => this.RequestRender();

	private void RequestRender() {

		if (Interlocked.Exchange(ref this._renderQueued, 1) == 1) {
			return;
		}

		_ = this.InvokeAsync(this.ProcessRender);

	}

	private void ProcessRender() {
		try {
			// Loop until no new changes arrive during processing.
			// Each iteration resets the flag first so that a concurrent
			// state change re-raises it, guaranteeing another pass.
			do {
				Interlocked.Exchange(ref this._renderQueued, 0);

				this.EnsureInitializationStarted();

				if (this.EvaluateState()) {
					this.StateHasChanged();
				}

			} while (Interlocked.CompareExchange(ref this._renderQueued, 0, 0) == 1);

			if (this.Orchestrator.HasCompleted) {
				this._activityStateSubscription?.Dispose();
				this._activityStateSubscription = null;
			}
		} catch (Exception) {
			// Reset so future state changes aren't permanently blocked.
			Interlocked.Exchange(ref this._renderQueued, 0);
			throw;
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

	private bool IsAuthenticationPath() {
		var relativePath = this.Navigation.ToBaseRelativePath(this.Navigation.Uri);
		return relativePath.StartsWith(this.AuthenticationBasePath, StringComparison.OrdinalIgnoreCase);
	}

	private ViewState ComputeViewState() {

		// 1. Authentication callback path — route normally with pending layout
		if (this.IsAuthenticationPath()) {
			return ViewState.AuthenticationPath;
		}

		// 2. Route requires auth + not authenticated — redirect to login, no layout or DOM rendered
		if (RouteRequiresAuthorization(this.RouteData.PageType) && !this.UserState.IsAuthenticated) {
			return ViewState.RedirectToLogin;
		}

		// 3. Initialization not yet complete — render the pending layout. The splash
		//    screen (typically hosted by the layout) covers application user loading,
		//    profile enrichment, and any registered initializable services, including
		//    remote state. We gate on HasCompleted rather than IsActive to avoid
		//    brief flickers when the activity state transitions between tasks.
		if (!this.Orchestrator.HasCompleted) {
			return ViewState.Pending;
		}

		// 4–5. Application user checks (only when a factory is registered)
		if (this._requiresApplicationUser) {

			if (this.UserState.ApplicationUser is null) {
				return ViewState.NotProvisioned;
			}

			if (!this.UserState.ApplicationUser.IsEnabled) {
				return ViewState.Disabled;
			}

		}

		// 6. All checks pass
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
	// Helpers
	// -------------------------------------------------------------------------

	private void EnsureInitializationStarted() {

		if (this.Orchestrator.HasStarted) {
			return;
		}

		if (this.IsAuthenticationPath()) {
			return;
		}

		if (RouteRequiresAuthorization(this.RouteData.PageType) && !this.UserState.IsAuthenticated) {
			return;
		}

		this.Orchestrator.Start();
	}

	// -------------------------------------------------------------------------
	// Disposal
	// -------------------------------------------------------------------------

	public void Dispose() {
		this._userStateSubscription?.Dispose();
		this._activityStateSubscription?.Dispose();
	}

	// -------------------------------------------------------------------------
	// Types
	// -------------------------------------------------------------------------

	private enum ViewState {
		AuthenticationPath,
		RedirectToLogin,
		Pending,
		NotProvisioned,
		Disabled,
		Ready
	}

}