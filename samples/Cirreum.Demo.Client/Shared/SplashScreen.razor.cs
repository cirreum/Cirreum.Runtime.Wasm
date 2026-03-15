namespace Cirreum.Demo.Client.Shared;

using Microsoft.AspNetCore.Components;

public partial class SplashScreen {

	string activityMessage = "";

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
	public bool Visible { get; set; } = false;
	[Parameter]
	public bool DisplayProgress { get; set; } = true;

	private bool IsVisible => this.Visible || this.State.IsActive;

	protected override void OnInitialized() {
		this.activityMessage = this.ActivityMessage;
		this.UpdateFromState();
	}

	protected override void OnStateChanged() {
		this.UpdateFromState();
		this.StateHasChanged();
	}

	private void UpdateFromState() {
		this.activityMessage = this.State.DisplayStatus ?? "";
	}

}