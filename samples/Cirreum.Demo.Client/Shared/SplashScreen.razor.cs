namespace Cirreum.Demo.Client.Shared;

using Microsoft.AspNetCore.Components;

public partial class SplashScreen {

	string activityMessage = "";
	bool isVisible;

	[Parameter]
	public string ActivityMessage { get; set; } = "";
	[Parameter]
	public string AppSplashTitle { get; set; } = "Championship Off-Road Racing";
	[Parameter]
	public string AppSplashSubtitle { get; set; } = "Race Management System";
	[Parameter]
	public string AppSplashMessage { get; set; } = "please wait";
	[Parameter]
	public string AppSplashLogoUrl { get; set; } = "/splash-screen-icon.png";
	[Parameter]
	public bool IsVisible { get; set; }
	[Parameter]
	public bool DisplayProgress { get; set; } = true;
	[Parameter]
	public bool IsIndeterminate { get; set; } = true;

	protected override void OnInitialized() {
		this.activityMessage = this.ActivityMessage;
		if (!string.IsNullOrWhiteSpace(this.State.DisplayStatus)) {
			this.activityMessage = this.State.DisplayStatus;
		}
		this.isVisible = this.IsVisible;
		if (this.State.IsInitializing && !this.isVisible) {
			this.isVisible = true;
		}
	}

	protected override void OnStateChanged() {
		var changed = false;
		if (!string.IsNullOrWhiteSpace(this.State.DisplayStatus)) {
			this.activityMessage = this.State.DisplayStatus;
			changed = true;
		}
		if (this.State.IsInitializing && !this.isVisible) {
			this.isVisible = true;
		}
		if (!this.State.IsInitializing && this.isVisible) {
			this.isVisible = false;
			changed = true;
		}
		if (changed) {
			this.StateHasChanged();
		}
	}

}