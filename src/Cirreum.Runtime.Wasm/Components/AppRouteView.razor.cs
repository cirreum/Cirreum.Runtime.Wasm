namespace Cirreum.Runtime.Components;

using Cirreum.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Application-level route view that enforces authentication and application user
/// readiness before rendering routed page components. Drop-in replacement for
/// <see cref="RouteView"/> or <see cref="AuthorizeRouteView"/> inside a
/// <see cref="Router"/>'s <c>&lt;Found&gt;</c> block.
/// </summary>
/// <remarks>
/// <para>
/// Renders <see cref="RouteView"/> for authentication callback paths and pending
/// states, and <see cref="AuthorizeRouteView"/> once the application is ready.
/// Page components are never instantiated until readiness is confirmed.
/// </para>
/// <para>
/// The state machine is re-evaluated on every <see cref="IUserState"/> change and
/// URL navigation. States are evaluated top-to-bottom; first match wins:
/// </para>
/// <list type="number">
///   <item><description>Authentication path → <see cref="RouteView"/> with <see cref="AuthenticationLayout"/></description></item>
///   <item><description>Not authenticated → <see cref="RedirectToLogin"/> inside <see cref="AuthenticationLayout"/></description></item>
///   <item><description>App user loading in progress → <see cref="AuthenticationLayout"/> splash covers the wait (only when <see cref="IApplicationUserLoader"/> is registered)</description></item>
///   <item><description>App user not found → <see cref="NotProvisioned"/> (only when <see cref="IApplicationUserLoader"/> is registered)</description></item>
///   <item><description>App user disabled → <see cref="Disabled"/> (only when <see cref="IApplicationUserLoader"/> is registered)</description></item>
///   <item><description>All checks pass → <see cref="AuthorizeRouteView"/> with <see cref="DefaultLayout"/></description></item>
/// </list>
/// </remarks>
public sealed partial class AppRouteView : ComponentBase, IDisposable {

	[Inject] private IUserState UserState { get; set; } = default!;
	[Inject] private IStateManager StateManager { get; set; } = default!;
	[Inject] private NavigationManager Navigation { get; set; } = default!;
	[Inject] private IServiceProvider ServiceProvider { get; set; } = default!;

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
	/// The layout rendered during authentication paths and pending states.
	/// Defaults to <see cref="DefaultLayout"/> when not specified. The splash
	/// screen typically lives in this layout and self-manages via initialization state.
	/// </summary>
	[Parameter]
	public Type? AuthenticationLayout { get; set; }

	/// <summary>
	/// The base-relative path prefix used for OIDC/MSAL authentication callbacks.
	/// Defaults to <c>"authentication"</c>, matching Blazor's standard
	/// <c>/authentication/{action}</c> route convention.
	/// </summary>
	[Parameter]
	public string AuthenticationBasePath { get; set; } = "authentication";

	/// <summary>
	/// Rendered when the application user loader completed but returned <see langword="null"/> —
	/// the identity is authenticated but has no account in this application.
	/// </summary>
	/// <remarks>
	/// Only evaluated when an <see cref="IApplicationUserLoader"/> is registered.
	/// When no loader is registered, transitions directly from authenticated to ready.
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
	public string? LoginPath { get; set; }

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
	public OidcPrompt? OidcPrompt { get; set; }

	// -------------------------------------------------------------------------
	// State
	// -------------------------------------------------------------------------

	private IDisposable? _userStateSubscription;
	private ViewState _viewState;
	private bool _requiresApplicationUser;

	private Type ResolvedAuthenticationLayout => this.AuthenticationLayout ?? this.DefaultLayout;

	// -------------------------------------------------------------------------
	// Lifecycle
	// -------------------------------------------------------------------------

	protected override void OnInitialized() {
		this._requiresApplicationUser = this.ServiceProvider.GetService<IApplicationUserLoader>() is not null;
		this._userStateSubscription = this.StateManager.Subscribe<IUserState>(this.OnUserStateChanged);
		this.Navigation.LocationChanged += this.OnLocationChanged;
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

	private void OnLocationChanged(object? sender, LocationChangedEventArgs args) {
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

		// 1. Authentication callback path — route normally
		var relativePath = this.Navigation.ToBaseRelativePath(this.Navigation.Uri);
		if (relativePath.StartsWith(this.AuthenticationBasePath, StringComparison.OrdinalIgnoreCase)) {
			return ViewState.AuthenticationPath;
		}

		// 2. Not authenticated — redirect to login
		if (!this.UserState.IsAuthenticated) {
			return ViewState.Pending;
		}

		// 3–5. Application user checks (only when a loader is registered)
		if (this._requiresApplicationUser) {

			// 3. Loader registered but not yet completed — splash covers the wait
			if (!this.UserState.IsApplicationUserLoaded) {
				return ViewState.Pending;
			}

			// 4. Load completed but no account exists in this application
			if (this.UserState.ApplicationUser is null) {
				return ViewState.NotProvisioned;
			}

			// 5. Account exists but is disabled
			if (!this.UserState.ApplicationUser.IsEnabled) {
				return ViewState.Disabled;
			}
		}

		// 6. All checks pass
		return ViewState.Ready;
	}

	// -------------------------------------------------------------------------
	// Disposal
	// -------------------------------------------------------------------------

	public void Dispose() {
		this.Navigation.LocationChanged -= this.OnLocationChanged;
		this._userStateSubscription?.Dispose();
	}

	// -------------------------------------------------------------------------
	// Types
	// -------------------------------------------------------------------------

	private enum ViewState {
		AuthenticationPath,
		Pending,
		NotProvisioned,
		Disabled,
		Ready
	}

}
