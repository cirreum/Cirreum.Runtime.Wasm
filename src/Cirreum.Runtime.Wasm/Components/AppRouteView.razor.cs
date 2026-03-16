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
/// Renders <see cref="LayoutView"/> during initialization and authentication (no page
/// component instantiated) and <see cref="AuthorizeRouteView"/> for authentication callback
/// paths. Once the application is ready, renders either <see cref="AuthorizeRouteView"/>
/// or <see cref="RouteView"/> depending on whether authentication services are registered.
/// Page components are never instantiated until readiness is confirmed.
/// </para>
/// <para>
/// The state machine is re-evaluated on every <see cref="IUserState"/> change,
/// <see cref="IActivityState"/> change, and <see cref="RouteData"/> update.
/// States are evaluated top-to-bottom; first match wins:
/// </para>
/// <list type="number">
///   <item><description><see cref="ViewState.Pending"/> — Initialization not yet complete → <see cref="LayoutView"/> with <see cref="PendingLayout"/> (no page component instantiated). Covers authentication, application user loading, profile enrichment, and any registered <see cref="IInitializable"/> services.</description></item>
///   <item><description><see cref="ViewState.AuthenticationPath"/> — URI matches <see cref="AuthenticationBasePath"/> → <see cref="AuthorizeRouteView"/> with <see cref="PendingLayout"/> so the authentication callback page can process the response.</description></item>
///   <item><description><see cref="ViewState.RedirectToLogin"/> — Route requires auth and user is not authenticated → <see cref="AuthorizeRouteView"/> with <see cref="PendingLayout"/> and <see cref="RedirectToLogin"/> in the <c>NotAuthorized</c> slot. The splash screen remains visible during the redirect.</description></item>
///   <item><description><see cref="ViewState.NotProvisioned"/> — Authenticated identity has no application account (only when <see cref="IApplicationUserFactory"/> is registered).</description></item>
///   <item><description><see cref="ViewState.Disabled"/> — Application user exists but <see cref="IApplicationUser.IsEnabled"/> is <see langword="false"/>.</description></item>
///   <item><description><see cref="ViewState.Ready"/> — All checks pass → <see cref="AuthorizeRouteView"/> (when auth registered) or <see cref="RouteView"/> with <see cref="DefaultLayout"/>.</description></item>
/// </list>
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
	private IDisposable? _activityStateSubscription;
	private ViewState _viewState = ViewState.Pending;
	private bool _requiresApplicationUser;
	private bool _hasAuthenticationRouting;
	private string? _redirectReturnUrl;

	private Type ResolvedPendingLayout => this.PendingLayout ?? this.DefaultLayout;

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	/// <inheritdoc />
	/// <remarks>
	/// Captures the current URI as the return URL for login redirects before
	/// delegating to the base implementation. Called on every parameter update
	/// (including re-renders triggered by the <see cref="Router"/>).
	/// </remarks>
	public override async Task SetParametersAsync(ParameterView parameters) {
		this._redirectReturnUrl = this.Navigation.Uri;
		await base.SetParametersAsync(parameters);
	}

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

		// Detect optional services that influence the state machine.
		this._requiresApplicationUser = this.ServiceProvider.GetService<IApplicationUserFactory>() is not null;
		this._hasAuthenticationRouting = this.ServiceProvider.GetService<AuthenticationStateProvider>() is not null;

		// Subscribe to state changes that drive view transitions.
		this._userStateSubscription = this.StateManager.Subscribe<IUserState>(this.OnUserStateChanged);
		this._activityStateSubscription = this.StateManager.Subscribe<IActivityState>(this.OnActivityStateChanged);
		this.Navigation.LocationChanged += this.OnLocationChanged;

		// Hold in Pending until authentication is resolved and orchestration completes.
		// This prevents any page component from rendering before the app is ready.
		this._viewState = ViewState.Pending;

		// Start activity immediately so splash shows from first frame
		// regardless of auth state or orchestrator status
		this.Activity.StartTask("Starting application...");

		// Start the orchestrator only when authentication state is already known.
		// For anonymous apps or post-callback returns, auth is complete immediately.
		// For auth apps on cold start, the orchestrator is started from OnUserStateChanged
		// once IsAuthenticationComplete flips to true — ensuring Phase 1 (app user loading,
		// profile enrichment) runs with the correct identity.
		if (!this._hasAuthenticationRouting || this.UserState.IsAuthenticationComplete) {
			this.Orchestrator.Start();
		}

	}

	/// <summary>
	/// Handles parameter updates and triggers a component re-render if the state has changed.
	/// </summary>
	/// <remarks>This method is called by the Blazor framework when component parameters are set. Override this
	/// method to respond to parameter changes. If the component's state changes as a result of new parameters, a re-render
	/// is requested.</remarks>
	protected override void OnParametersSet() {
		if (this.EvaluateState()) {
			this.StateHasChanged();
		}
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
		this.TryStartOrchestrator();
		if (this.EvaluateState()) {
			this.InvokeAsync(this.StateHasChanged);
		}
	}

	/// <summary>
	/// Called when <see cref="IActivityState"/> changes (task progress, completion).
	/// Re-evaluates the state machine and triggers a render only when the view state changed.
	/// </summary>
	private void OnActivityStateChanged(IActivityState _) {
		if (this.EvaluateState()) {
			this.InvokeAsync(this.StateHasChanged);
		}
	}

	/// <summary>
	/// Handles the event that occurs when the location has changed.
	/// </summary>
	/// <param name="sender">The source of the event. This is typically the object that raised the event.</param>
	/// <param name="e">The event data containing information about the location change.</param>
	private void OnLocationChanged(object? sender, LocationChangedEventArgs e) {
		this.TryStartOrchestrator();
		if (this.EvaluateState()) {
			this.InvokeAsync(this.StateHasChanged);
		}
	}

	/// <summary>
	/// Attempts to start the orchestrator if all required authentication and routing conditions are met.
	/// </summary>
	/// <remarks>This method checks whether the orchestrator has already started, whether user authentication is
	/// complete, and whether the current route requires authorization. The orchestrator is only started if all these
	/// conditions are satisfied. This method does not throw exceptions if the orchestrator cannot be started; it simply
	/// performs no action.</remarks>
	private void TryStartOrchestrator() {
		if (!this.Orchestrator.HasStarted
			&& this.UserState.IsAuthenticationComplete
			&& !this.IsAuthenticationPath()
			&& !(RouteRequiresAuthorization(this.RouteData.PageType) && !this.UserState.IsAuthenticated)) {
			this.Orchestrator.Start();
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

		// 1. Authentication callback — let the auth page process the IdP response
		//    while the splash screen (PendingLayout) stays visible behind it.
		if (this.IsAuthenticationPath()) {
			return ViewState.AuthenticationPath;
		}

		// 2. Route requires auth + user not authenticated — redirect inside the
		//    PendingLayout so the splash remains visible during the MSAL/OIDC redirect.
		if (RouteRequiresAuthorization(this.RouteData.PageType) && !this.UserState.IsAuthenticated) {
			return ViewState.RedirectToLogin;
		}

		// 3. Initialization not yet complete — render the PendingLayout (splash).
		//    Gates on HasCompleted (not IsActive) to avoid brief flickers when
		//    the activity state transitions between tasks.
		if (!this.Orchestrator.HasCompleted) {
			return ViewState.Pending;
		}

		// 4–5. Application user checks (only when IApplicationUserFactory is registered).
		if (this._requiresApplicationUser) {

			if (this.UserState.ApplicationUser is null) {
				return ViewState.NotProvisioned;
			}

			if (!this.UserState.ApplicationUser.IsEnabled) {
				return ViewState.Disabled;
			}

		}

		// 6. All checks pass — render the target page with the default layout.
		return ViewState.Ready;

	}

	/// <summary>
	/// Returns <see langword="true"/> when the current URI starts with
	/// <see cref="AuthenticationBasePath"/> (e.g. <c>/authentication/login-callback</c>).
	/// </summary>
	private bool IsAuthenticationPath() {
		var relativePath = this.Navigation.ToBaseRelativePath(this.Navigation.Uri);
		return relativePath.StartsWith(this.AuthenticationBasePath, StringComparison.OrdinalIgnoreCase);
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
		this.Navigation.LocationChanged -= this.OnLocationChanged;
		this._userStateSubscription?.Dispose();
		this._activityStateSubscription?.Dispose();
	}

	// -------------------------------------------------------------------------
	// Types
	// -------------------------------------------------------------------------

	/// <summary>
	/// The possible states of the <see cref="AppRouteView"/> state machine.
	/// Evaluated top-to-bottom by <see cref="ComputeViewState"/>; first match wins.
	/// </summary>
	private enum ViewState {
		/// <summary>URI matches <see cref="AuthenticationBasePath"/> — render auth callback page.</summary>
		AuthenticationPath,
		/// <summary>Route requires auth, user not authenticated — redirect to login.</summary>
		RedirectToLogin,
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