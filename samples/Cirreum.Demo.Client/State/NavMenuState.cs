namespace Cirreum.Demo.Client.State;

using Cirreum.Storage;

public sealed class NavMenuState(
	ILocalStorageService storage,
	IStateManager stateManager,
	IJSAppModule js
) : ScopedNotificationState
  , INavMenuState {

	private const string IsNavMinimalModelKey = "corr-nav-mini-mode";

	public async Task HideNavMenuAsync(bool animate = true) {
		if (this.IsNavMenuShown) {
			if (animate) {
				js.SwapElementClass(NavMenuSelectors.NavMenuOffCanvasSelector, NavMenuSelectors.SHOW, NavMenuSelectors.HIDING);
				js.SwapElementClass(NavMenuSelectors.NavMenuOffCanvasBackdropSelector, NavMenuSelectors.SHOW, NavMenuSelectors.HIDE);
				await Task.Delay(500);
				js.RemoveElementClass(NavMenuSelectors.NO_SCROLL_CONTAINER, NavMenuSelectors.NO_SCROLL_CLASS);
				js.RemoveElementClass(NavMenuSelectors.NavMenuOffCanvasSelector, NavMenuSelectors.HIDING);
			} else {
				js.SwapElementClass(NavMenuSelectors.NavMenuOffCanvasBackdropSelector, NavMenuSelectors.SHOW, NavMenuSelectors.HIDE);
				js.RemoveElementClass(NavMenuSelectors.NO_SCROLL_CONTAINER, NavMenuSelectors.NO_SCROLL_CLASS);
				js.RemoveElementClass(NavMenuSelectors.NavMenuOffCanvasSelector, NavMenuSelectors.SHOW);
			}
			this.SetIsNavMenuShown(false);
		}
	}
	public async Task ShowNavMenuAsync() {
		js.SetElementClassIfScrollbar(NavMenuSelectors.NO_SCROLL_CONTAINER, true, NavMenuSelectors.NO_SCROLL_CLASS);
		js.SwapElementClass(NavMenuSelectors.NavMenuOffCanvasBackdropSelector, NavMenuSelectors.HIDE, NavMenuSelectors.SHOW);
		js.AddElementClass(NavMenuSelectors.NavMenuOffCanvasSelector, NavMenuSelectors.SHOWING);
		await Task.Delay(200);
		js.SwapElementClass(NavMenuSelectors.NavMenuOffCanvasSelector, NavMenuSelectors.SHOWING, NavMenuSelectors.SHOW);
		this.SetIsNavMenuShown(true);
	}
	public async Task ToggleNavMenu() {
		if (this.IsNavMenuShown) {
			await this.HideNavMenuAsync();
			return;
		}
		await this.ShowNavMenuAsync();
		js.ScrollElementIntoView(
			NavMenuSelectors.ActiveNavLinkSelector,
			ScrollBehavior.Instant,
			ScrollLogicalPosition.Nearest,
			ScrollLogicalPosition.Start);
	}

	public bool IsNavMenuShown { get; private set; }
	public void SetIsNavMenuShown(bool value) {
		using var _ = this.CreateNotificationScope();
		this.IsNavMenuShown = value;
	}

	public string NavMenuScrollContainer { get; private set; } = NavMenuSelectors.NavMenuOffCanvasBodySelector;
	public void SetNavMenuScrollContainer(string value) {
		using var _ = this.CreateNotificationScope();
		this.NavMenuScrollContainer = value;
	}

	public bool IsMinimalMode { get; private set; } = false;
	public async Task InitializeMinimalModeAsync() {
		var storedValue = await storage.GetItemAsync<bool?>(IsNavMinimalModelKey);
		this.IsMinimalMode = storedValue ?? false;
	}
	public async Task ToggleMinimalModeAsync() {
		using var _ = this.CreateNotificationScope();
		this.IsMinimalMode = !this.IsMinimalMode;
		await storage.SetItemAsync(IsNavMinimalModelKey, this.IsMinimalMode);
	}

	protected override void OnStateHasChanged() {
		stateManager.NotifySubscribers<INavMenuState>(this);
	}

}