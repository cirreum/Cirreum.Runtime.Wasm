namespace Cirreum.Demo.Client.State;

public interface INavMenuState : IScopedNotificationState {

	Task HideNavMenuAsync(bool animate = true);
	Task ShowNavMenuAsync();
	Task ToggleNavMenu();

	bool IsNavMenuShown { get; }
	void SetIsNavMenuShown(bool value);

	string NavMenuScrollContainer { get; }
	void SetNavMenuScrollContainer(string value);

	bool IsMinimalMode { get; }
	Task InitializeMinimalModeAsync();
	Task ToggleMinimalModeAsync();

}