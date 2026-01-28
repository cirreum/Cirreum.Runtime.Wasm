namespace Cirreum.Demo.Client;

public static class Routes {

	public const string Index = "/";
	public const string ViewModels = "/view-models";
	public const string Weather = "/weather";
	public const string Dialogs = "/dialogs";
	public const string Forms = "/form-control";
	public const string Detail = "/detail";
	public const string Theme = "/theme";
	public const string Tabs = "/tabs";
	public const string BrowserInfo = "/browser-info";
	public const string TimeZoneInfo = "/timezone-tests";
	public const string Authentication = "authentication";
	public const string Login = $"{Authentication}/login";
	public const string Logout = $"{Authentication}/logout";
	public const string TermsOfService = "terms";
	public const string PrivacyPolicy = "privacy";
	public const string Unauthorized = "unauthorized";
	public const string Error = "error";
	public const string EmptyData = "empty-data";
	public const string NullData = "null-data";
	public const string SimpleGrid = "simple-grid";
	public const string UtilComponents = "utility-components";
	public const string ClientAuth = "client-authorization";

	// Navigation metadata
	public static readonly List<NavRoute> MainNavRoutes = [
		new(Index, "house", "Home"),
		new(ClientAuth, "shield", "Client Auth"),
		new(Theme, "card-list", "Theme"),
		new(SimpleGrid, "star", "Simple Grid"),
		new(Detail, "list-task", "Detail Grid"),
		new(Dialogs, "window-stack", "Dialogs"),
		new(Tabs, "folder2", "Tabs"),
		new("/profile-card-test", "person-vcard", "Profile Card"),
		new("/profile-card-debug", "bug", "Profile Debug"),
		new(Forms, "check-square", "Forms"),
		new(ViewModels, "diagram-3", "View Models"),
		new(UtilComponents, "window-stack", "Misc"),
		new(EmptyData, "graph-down", "Empty Data"),
		new(NullData, "file-earmark", "Null Data"),
		new(Weather, "list-nested", "Weather"),
		new(TimeZoneInfo, "calendar2-check", "Time-Zone"),
		new(BrowserInfo, "browser-edge", "Browser Formats")
	];

	public static class Communications {
		public const string Root = "communications";
		public const string Index = $"{Root}/";
		public static readonly NavRoute Nav = new(Index, "chat-left-text", "Communications");
	}

	public static class Users {
		public const string Root = "users";
		public const string Index = $"{Root}/";
		public const string Profile = $"{Root}/profile";
		public static readonly NavRoute ProfileNav = new(Profile, "person-gear", "Profile");
	}

	public static class Management {
		public const string Root = "manage";
		public const string Index = $"{Root}/";
		public const string Events = $"{Root}/events";
		public const string Entries = $"{Root}/entries";
		public const string SmsTemplates = $"{Root}/smstemplates";

		public static readonly List<NavRoute> NavRoutes = [
			new(Index, "building-gear", "Manage"),
			new(Events, "calendar-event", "All Events"),
			new(Entries, "ticket-detailed-fill", "All Entries"),
			new(SmsTemplates, "file-text", "Sms Templates")
		];
	}
}

public record NavRoute(string Path, string Icon, string Name, bool MatchAll = true);