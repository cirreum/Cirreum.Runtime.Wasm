namespace Cirreum.Components;

using Cirreum.Security;
using Cirreum.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
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
/// Renders the <see cref="PendingLayout"/> during authentication and
/// initialization — no page component is instantiated until readiness is confirmed.
/// Once ready, renders either <see cref="AuthorizeRouteView"/> or <see cref="RouteView"/>
/// depending on whether authentication services are registered.
/// </para>
/// <para>
/// The state machine is re-evaluated on every <see cref="IUserState"/> change,
/// <see cref="IActivityState"/> change, and <see cref="RouteData"/> update.
/// </para>
/// <para>
/// States are evaluated top-to-bottom; first match wins:
/// <list type="number">
///   <item><description><see cref="ViewState.Pending"/> - Authentication is in-flight or the orchestrator has not completed → <see cref="PendingLayout"/>. Covers the full startup sequence: auth callback processing, application user loading, profile enrichment, and registered <see cref="IInitializable"/> services.</description></item>
///   <item><description><see cref="ViewState.NotProvisioned"/> - Authenticated identity has no application account (only when <see cref="IApplicationUserResolver"/> is registered).</description></item>
///   <item><description><see cref="ViewState.Disabled"/> - Application user exists but <see cref="IApplicationUser.IsEnabled"/> is <see langword="false"/>.</description></item>
///   <item><description><see cref="ViewState.Ready"/> - All checks pass → <see cref="AuthorizeRouteView"/> (when auth registered) or <see cref="RouteView"/>, with <see cref="DefaultLayout"/>.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class AppRouteView : ComponentBase, IDisposable {

	[Inject] private IUserState UserState { get; set; } = default!;
	[Inject] private IStateManager StateManager { get; set; } = default!;
	[Inject] private NavigationManager Navigation { get; set; } = default!;
	[Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
	[Inject] private IActivityState Activity { get; set; } = default!;
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
	/// The root path segment used to identify OIDC/MSAL authentication callback routes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Defaults to <c>"authentication"</c>, matching Blazor's standard
	/// <c>/authentication/{action}</c> route convention. Matched as a full segment —
	/// <c>"authentication"</c> matches <c>/authentication</c> and
	/// <c>/authentication/login-callback</c> but not <c>/authentication-settings</c>.
	/// </para>
	/// <para>
	/// <strong>Warning:</strong> Provide a single root segment only (e.g. <c>"auth"</c>) — sub-paths such as
	/// <c>"auth/login"</c> will only match routes nested under that specific path,
	/// not the entire authentication section.
	/// </para>
	/// <para>
	/// Trailing slashes are ignored.
	/// </para>
	/// </remarks>
	[Parameter]
	public string AuthenticationBasePath { get; set; } = "authentication";

	/// <summary>
	/// Rendered when the application user factory completed but returned <see langword="null"/> —
	/// the identity is authenticated but has no account in this application.
	/// </summary>
	/// <remarks>
	/// Only evaluated when an <see cref="IApplicationUserResolver"/> is registered.
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

	/// <summary>
	/// Gets or sets the content to display while authorization is in progress.
	/// </summary>
	/// <remarks>Use this property to provide custom UI or messages that are shown to the user during the
	/// authorization process. This content is rendered only when the authorization state is being determined and is not
	/// yet complete.</remarks>
	[Parameter]
	public RenderFragment? AuthorizingContent { get; set; }

	/// <summary>
	/// When <see langword="true"/>, the page component is instantiated during
	/// <see cref="ViewState.Pending"/> activity and rendered in the <see cref="PendingLayout"/>.
	/// When <see langword="false"/> (default), a <see cref="LayoutView"/>
	/// is rendered during pending states to prevent page instantiation entirely.
	/// </summary>
	/// <remarks>
	/// Only applies to non-authenticated apps. Authenticated apps always use
	/// <see cref="AuthorizeRouteView"/> which gates page instantiation via auth state.
	/// Enable only when page components are safe to instantiate before
	/// <see cref="IInitializable"/> services have completed.
	/// </remarks>
	[Parameter]
	public bool AllowEarlyPageInstantiation { get; set; } = false;

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

	private static readonly HashSet<string> TerminalAuthActions = new(StringComparer.OrdinalIgnoreCase) {
		"login-failed",
		"logout-failed",
		"logged-out"
	};
	private readonly CancellationTokenSource _cts = new();
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

	/// <summary>
	/// Initializes the component and sets up state subscriptions, navigation event handlers, and application startup
	/// logic.
	/// </summary>
	/// <remarks>This method configures the component's dependencies and subscribes to relevant state changes
	/// required for view transitions. It ensures that the application does not render page components until authentication
	/// and orchestration are complete, providing a consistent startup experience. The orchestrator is started immediately
	/// if authentication is already resolved, or deferred until authentication completes for applications requiring
	/// authentication.</remarks>
	protected override void OnInitialized() {
		// Pre-Render, Single (one-time) execution

		// Detect optional services that influence the state machine.
		this._requiresApplicationUser = this.ServiceProvider.GetService<IApplicationUserResolver>() is not null;
		this._hasAuthenticationRouting = this.ServiceProvider.GetService<AuthenticationStateProvider>() is not null;

		// Subscribe to state changes that drive view transitions.
		this._userStateSubscription = this.StateManager.Subscribe<IUserState>(this.OnUserStateChanged);
		this._activityStateSubscription = this.StateManager.Subscribe<IActivityState>(this.OnActivityStateChanged);

		// Kick of an activity for auth apps on cold start to ensure the splash screen
		// is shown while authentication is in-flight.
		if (this._hasAuthenticationRouting
			&& (RouteRequiresAuthorization(this.RouteData.PageType)
			|| this.IsAuthenticationPath())) {
			this.Activity.StartTask("Starting application...");
		}

		// Start the orchestrator only when authentication state is already known.
		// For anonymous apps or post-callback return.
		// For auth apps on cold start, the orchestrator is started from OnUserStateChanged
		// once IsAuthenticationComplete flips to true — ensuring Phase 1 (app user loading,
		// profile enrichment) runs with the correct identity.
		if (!this._hasAuthenticationRouting || this.UserState.IsAuthenticationComplete) {
			this.Orchestrator.Start(this._cts.Token);
		}

	}

	protected override void OnParametersSet() {
		this._redirectReturnUrl = this.Navigation.Uri;
		this.StartAuthPathActivityIfNeeded();
		this.TryStartOrchestrator();
		this.EvaluateState();
	}

	// -------------------------------------------------------------------------
	// Event Handlers
	// -------------------------------------------------------------------------

	/// <summary>
	/// Called when <see cref="IUserState"/> changes (authentication state, application user).
	/// Starts the <see cref="IInitializationOrchestrator"/> once authentication is complete
	/// and the app is not mid-redirect. Re-evaluates the state machine and triggers a render
	/// only when the view state changed.
	/// </summary>
	private void OnUserStateChanged(IUserState _) {
		// Post-Render

		// During Pending, protected routes render behind a LayoutView rather than
		// AuthorizeRouteView, so the built-in NotAuthorized → RedirectToLogin path
		// never fires. When auth settles as unauthenticated on a protected route,
		// we must redirect to login directly rather than relying on the razor to handle it.
		if (this._hasAuthenticationRouting
			&& this.UserState.IsAuthenticationComplete
			&& !this.UserState.IsAuthenticated
			&& RouteRequiresAuthorization(this.RouteData.PageType)) {
			this.RedirectToLoginDirect();
			return;
		}

		this.TryStartOrchestrator();

		if (this.EvaluateState()) {
			// Snapshot the current URI as the post-login return URL at the moment we detect
			// the user is unauthenticated on a protected route — the same condition that will
			// cause AuthorizeRouteView to render RedirectToLogin. Captured here before any
			// subsequent navigation changes the URI.
			if (RouteRequiresAuthorization(this.RouteData.PageType) && !this.UserState.IsAuthenticated) {
				this._redirectReturnUrl = this.Navigation.Uri;
			}
			this.InvokeAsync(this.StateHasChanged);
		}

	}

	/// <summary>
	/// Called when <see cref="IActivityState"/> changes (task progress, completion).
	/// Re-evaluates the state machine and triggers a render only when the view state changed.
	/// </summary>
	private void OnActivityStateChanged(IActivityState _) {
		// Post-Render
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

	/// <summary>
	/// Pure function — no side effects. Evaluates the current state and returns
	/// the matching <see cref="ViewState"/>. Evaluated top-to-bottom; first match wins.
	/// </summary>
	private ViewState ComputeViewState() {

		// 1. Auth in-flight — stay on Pending layout.
		if (this._hasAuthenticationRouting
			&& (this.IsAuthenticationPath()
				|| (RouteRequiresAuthorization(this.RouteData.PageType) && !this.UserState.IsAuthenticationComplete))) {
			return ViewState.Pending;
		}

		// 2. Initialization not yet complete — render the PendingLayout.
		//    Gates on HasCompleted (not IsActive) to avoid brief flickers when
		//    the activity state transitions between tasks.
		if (!this.Orchestrator.HasCompleted) {
			return ViewState.Pending;
		}

		// 3–4. Application user checks (only when IApplicationUserResolver is registered).
		if (this._requiresApplicationUser
			&& this.UserState.IsAuthenticationComplete
			&& this.UserState.IsAuthenticated) {

			if (this.UserState.ApplicationUser is null) {
				return ViewState.NotProvisioned;
			}

			if (!this.UserState.ApplicationUser.IsEnabled) {
				return ViewState.Disabled;
			}

		}

		// 5. All checks pass — render the target page with the default layout.
		return ViewState.Ready;

	}

	/// <summary>
	/// Returns <see langword="true"/> when the current URI matches or is nested under
	/// <see cref="AuthenticationBasePath"/> as a root path segment
	/// (e.g. <c>authentication</c> or <c>authentication/login-callback</c>).
	/// </summary>
	private bool IsAuthenticationPath() {
		var basePath = this.AuthenticationBasePath.TrimEnd('/');
		var relativePath = this.Navigation.ToBaseRelativePath(this.Navigation.Uri);
		return relativePath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)
			|| relativePath.Equals(basePath, StringComparison.OrdinalIgnoreCase);
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

	/// <summary>
	/// Attempts to start the <see cref="IInitializationOrchestrator"/> if all preconditions are met.
	/// </summary>
	/// <remarks>
	/// The orchestrator is started only once — guarded by <see cref="IInitializationOrchestrator.HasStarted"/>.
	/// Preconditions intentionally mirror the <see cref="ViewState.Pending"/> checks in
	/// <see cref="ComputeViewState"/>: authentication must be complete, the current path must
	/// not be an authentication callback, and the route must not require auth for an
	/// unauthenticated user. This ensures the orchestrator only starts when the same conditions
	/// that would allow a transition out of <see cref="ViewState.Pending"/> are true —
	/// preventing Phase 1 from running against an unsettled identity.
	/// The component's <see cref="CancellationToken"/> is passed to the orchestrator so
	/// initialization is cancelled if the component is disposed.
	/// </remarks>
	private void TryStartOrchestrator() {

		var isReady = !this.Orchestrator.HasStarted
			&& this.UserState.IsAuthenticationComplete
			&& !this.IsAuthenticationPath();

		var isRouteAccessible = !RouteRequiresAuthorization(this.RouteData.PageType)
			|| this.UserState.IsAuthenticated;

		if (isReady && isRouteAccessible) {
			this.Orchestrator.Start(this._cts.Token);
		}

	}

	/// <summary>
	/// Initiates an activity to display a splash screen when navigating to authentication-related routes, ensuring
	/// appropriate user feedback during authentication callbacks.
	/// </summary>
	/// <remarks>This method is typically used in authentication flows such as OpenID Connect (OIDC) where external
	/// redirects may result in longer processing times. By starting an activity with a relevant status message, it
	/// provides visual feedback to users during login, logout, and their respective callback routes. The method only
	/// triggers the activity if authentication routing is enabled and the current navigation path matches a recognized
	/// authentication route.</remarks>
	private void StartAuthPathActivityIfNeeded() {
		if (!this._hasAuthenticationRouting) {
			return;
		}

		var relativePath = this.Navigation.ToBaseRelativePath(this.Navigation.Uri);
		var basePath = this.AuthenticationBasePath.TrimEnd('/');
		var action = relativePath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)
			? relativePath[(basePath.Length + 1)..]
			: null;

		if (action is null) {
			return;
		}

		var message = action switch {
			var a when a.Equals("login", StringComparison.OrdinalIgnoreCase) => "Logging in...",
			var a when a.Equals("login-callback", StringComparison.OrdinalIgnoreCase) => "Signing in...",
			var a when a.Equals("logout", StringComparison.OrdinalIgnoreCase) => "Logging out...",
			var a when a.Equals("logout-callback", StringComparison.OrdinalIgnoreCase) => "Logging out...",
			_ => null
		};

		if (message is not null) {
			this.Activity.StartTask(message);
		} else if (TerminalAuthActions.Contains(action)) {
			this.Activity.ResetTasks();
		}
	}

	private void RedirectToLoginDirect() {

		InteractiveRequestOptions requestOptions = new() {
			Interaction = InteractionType.SignIn,
			ReturnUrl = this.Navigation.Uri
		};

		if (!string.IsNullOrWhiteSpace(this.LoginHint)) {
			requestOptions.TryAddAdditionalParameter("loginHint", this.LoginHint);
		}

		if (!string.IsNullOrWhiteSpace(this.DomainHint)) {
			requestOptions.TryAddAdditionalParameter("domainHint", this.DomainHint);
		}

		if (this.LoginPrompt.HasValue) {
			var promptValue = this.LoginPrompt.Value switch {
				OidcPrompt.None => "none",
				OidcPrompt.Login => "login",
				OidcPrompt.Consent => "consent",
				OidcPrompt.SelectAccount => "select_account",
				_ => null
			};

			if (promptValue is not null) {
				requestOptions.TryAddAdditionalParameter("prompt", promptValue);
			}
		}

		this.Navigation.NavigateToLogin(this.LoginPath ?? "authentication/login", requestOptions);
	}

	// -------------------------------------------------------------------------
	// Disposal
	// -------------------------------------------------------------------------

	public void Dispose() {
		this._userStateSubscription?.Dispose();
		this._activityStateSubscription?.Dispose();
		this._cts.Cancel();
		this._cts.Dispose();
	}

	// -------------------------------------------------------------------------
	// Types
	// -------------------------------------------------------------------------

	/// <summary>
	/// The possible states of the <see cref="AppRouteView"/> state machine.
	/// Evaluated top-to-bottom by <see cref="ComputeViewState"/>; first match wins.
	/// </summary>
	private enum ViewState {
		/// <summary>Orchestrator has not completed — show splash screen.</summary>
		Pending,
		/// <summary>Authenticated but no application user account found.</summary>
		NotProvisioned,
		/// <summary>Application user exists but is disabled.</summary>
		Disabled,
		/// <summary>All checks pass — render the target page.</summary>
		Ready
	}

}