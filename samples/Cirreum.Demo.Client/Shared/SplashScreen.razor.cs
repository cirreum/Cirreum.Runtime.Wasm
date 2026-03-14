namespace Cirreum.Demo.Client.Shared;

using Microsoft.AspNetCore.Components;

public partial class SplashScreen {

	string activityMessage = "";
	bool isIndeterminate = true;

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
	public bool IsVisible { get; set; } = true;
	[Parameter]
	public bool DisplayProgress { get; set; } = true;

	protected override void OnInitialized() {
		this.activityMessage = this.ActivityMessage;
		this.UpdateFromState();
	}

	protected override void OnStateChanged() {
		this.UpdateFromState();
		this.StateHasChanged();
	}

	private void UpdateFromState() {
		if (!string.IsNullOrWhiteSpace(this.State.DisplayStatus)) {
			this.activityMessage = this.State.DisplayStatus;
		}
		// Deterministic progress when orchestrator has multiple tasks running
		this.isIndeterminate = this.State.TotalTasks <= 1;
	}

}
