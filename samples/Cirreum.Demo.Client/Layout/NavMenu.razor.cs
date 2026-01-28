namespace Cirreum.Demo.Client.Layout;

using Microsoft.AspNetCore.Components.Routing;

public partial class NavMenu {

	private string ContainerClassList => CssBuilder
		.Default(NavMenuSelectors.NavMenuContainerClass)
			.AddClass("minimal-mode", when: this.NavMenuState.IsMinimalMode)
		.Build();

	private async void CloseOffCanvas() {
		if (this.NavMenuState.IsNavMenuShown) {
			await this.NavMenuState.HideNavMenuAsync(false);
		}
	}

	private static string GetBreakPointContainer(Breakpoint breakpoint) =>
			breakpoint == Breakpoint.Large
				? NavMenuSelectors.NavMenuContainerSelector
				: NavMenuSelectors.NavMenuOffCanvasBodySelector;


	private void SetScrollContainer(Breakpoint breakpoint) {
		var activeContainer = GetBreakPointContainer(breakpoint);
		this.NavMenuState.SetNavMenuScrollContainer(activeContainer);
	}

	private async ValueTask BreakpointChanged(BreakpointChangeEventArgs e) {
		if (e.ChangedBreakpoint == Breakpoint.Large) {
			var isNowLarge = e.IsActive ? Breakpoint.Large : Breakpoint.Small;
			this.SetScrollContainer(isNowLarge);
			if (this.NavMenuState.IsNavMenuShown) {
				await this.NavMenuState.HideNavMenuAsync(false);
			}
			if (isNowLarge == Breakpoint.Large) {
				this.JS.ScrollElementIntoView(
					NavMenuSelectors.ActiveNavLinkSelector,
					ScrollBehavior.Instant,
					ScrollLogicalPosition.Nearest,
					ScrollLogicalPosition.Start);
			}
		}
	}
	private async void LocationChanged(object? sender, LocationChangedEventArgs e) {
		if (this.NavMenuState.IsNavMenuShown) {
			await this.NavMenuState.HideNavMenuAsync(false);
		}
	}

	protected override void OnInitialized() {

		this.NavigationManager.LocationChanged += this.LocationChanged;

		this.BreakpointMonitor.MinBreakPointChanged += this.BreakpointChanged;

		this.SubscribeToStateChanges<IUserState>();
		this.SubscribeToStateChanges<INavMenuState>();

	}

	protected override void OnAfterRender(bool firstRender) {
		if (firstRender) {
			var isLarge = this.BreakpointMonitor.CheckForBreakPoint(Breakpoint.Large);
			var activeContainer = GetBreakPointContainer(isLarge ? Breakpoint.Large : Breakpoint.Small);
			if (this.NavMenuState.NavMenuScrollContainer != activeContainer) {
				this.NavMenuState.SetNavMenuScrollContainer(activeContainer);
			}
			this.JS.ScrollElementIntoView(
				NavMenuSelectors.ActiveNavLinkSelector,
				ScrollBehavior.Instant,
				ScrollLogicalPosition.Nearest,
				ScrollLogicalPosition.Start);
		}
	}

	protected override void Dispose(bool disposing) {
		this.NavigationManager.LocationChanged -= this.LocationChanged;
		this.BreakpointMonitor.MinBreakPointChanged -= this.BreakpointChanged;
		base.Dispose(disposing);
	}

}