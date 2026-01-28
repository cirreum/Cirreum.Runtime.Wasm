namespace Cirreum.Demo.Client;

public interface ITabRenderModeState : IScopedNotificationState {
	TabsRenderMode TabsRenderMode { get; }
	void SetTabsRenderMode(TabsRenderMode tabsRenderMode);
}