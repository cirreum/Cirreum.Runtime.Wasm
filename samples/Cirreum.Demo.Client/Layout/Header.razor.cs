namespace Cirreum.Demo.Client.Layout;

using Microsoft.AspNetCore.Components;

public enum AccountMenuAction {
	Logout,
	NavToProfile,
	NavToManagement
}

public partial class Header(
	IJSAppModule js,
	INavMenuState navState,
	IPageState displayState,
	INotificationState notificationState
) : StateComponentBase {

	/// <summary>
	/// Gets or sets if the navigation side-bar is shown/visible. Default: <see langword="true"/>
	/// </summary>
	[Parameter]
	public bool IsNavMenuVisible { get; set; } = true;

	//private string UserDisplayName =>
	//	user.Profile.DisplayName ??
	//	user.Name ??
	//	"Guest";

	//private string UserEmail =>
	//	user.Profile.Email ??
	//	"";

	//private string UserAvatarUrl =>
	//	user.Profile.Picture ??
	//	"";

	//private string CompanyName =>
	//	user.Profile.Organization.OrganizationName ??
	//	user.Profile.Company ??
	//	$"{user.Provider}" ?? "";

	private readonly string NotificationsId = IdGenerator.Next;
	private readonly string NotificationsLabelId = IdGenerator.Next;
	private string NotificationsOffCanvasCss => CssBuilder
		.Default("offcanvas")
			.AddClass("offcanvas-end")
			.AddClass("show", when: this.NotificationsShow)
		.Build();
	private string NotificationsOffCanvasBackdropCss => CssBuilder
		.Default("offcanvas-backdrop fade")
			.AddClass("show", when: this.NotificationsShow)
			.AddClass("collapse", when: this.NotificationsShow is false)
		.Build();
	private bool NotificationsShow { get; set; } = false;
	private void ShowNotifications() {
		if (this.NotificationsShow is false) {
			this.NotificationsShow = true;
			this.StateHasChanged();
			js.SetElementClassIfScrollbar("body", true, "noscroll");
		}
		notificationState.Refresh();
	}
	private void HideNotifications() {
		if (this.NotificationsShow) {
			this.NotificationsShow = false;
			this.StateHasChanged();
			js.RemoveElementClass("body", "noscroll");
		}
	}

	static class LauncherUrls {
		public const string CorporateSite = "https://corracing.com/";
		public const string Registration = "https://speedhive.mylaps.com/Organizations/528574";
	}
	private async void OnLauncherUrlClicked(string url) {
		await js.InvokeVoidAsync("open", url, "_blank");
	}

	//private void OnAccountMenuSelected(AccountMenuAction value) {
	//	switch (value) {

	//		case AccountMenuAction.Logout:
	//			navManager.NavigateToLogout(Routes.Logout, navManager.BaseUri);
	//			break;

	//		case AccountMenuAction.NavToProfile:
	//			navManager.NavigateTo(Routes.Users.Profile);
	//			break;

	//		case AccountMenuAction.NavToManagement:
	//			navManager.NavigateTo(Routes.Management.Index);
	//			break;

	//		default:
	//			break;
	//	}
	//}

	protected override void OnInitialized() {
		this.SubscribeToStateChanges<IUserState>();
		this.SubscribeToStateChanges<IPageState>();
		this.SubscribeToStateChanges<INotificationState>();
	}

}