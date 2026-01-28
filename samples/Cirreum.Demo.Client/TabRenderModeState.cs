namespace Cirreum.Demo.Client;

public class TabRenderModeState(
	IStateManager stateManager
) : ScopedNotificationState
  , ITabRenderModeState {

	public TabsRenderMode TabsRenderMode { get; private set; }

	public void SetTabsRenderMode(TabsRenderMode tabsRenderMode) {
		using var _ = this.CreateNotificationScope();
		this.TabsRenderMode = tabsRenderMode;
	}

	protected override void OnStateHasChanged() {
		stateManager.NotifySubscribers<ITabRenderModeState>(this);
	}

}