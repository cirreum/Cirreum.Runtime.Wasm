namespace Cirreum.Components.Presence;

internal class UserPresenceIcons {

	//
	//Filled(Normal)
	//

	// Available
	public static string NormalAvailable =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-available);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12 24a12 12 0 1 0 0-24 12 12 0 0 0 0 24Zm5.06-13.44-5.5 5.5a1.5 1.5 0 0 1-2.12 0l-2-2a1.5 1.5 0 0 1 2.12-2.12l.94.94 4.44-4.44a1.5 1.5 0 0 1 2.12 2.12Z""></path></svg>";

	// Away
	public static string NormalAway =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-away);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12 24a12 12 0 1 0 0-24 12 12 0 0 0 0 24Zm1-17.5v4.81l2.98 2.55a1.5 1.5 0 0 1-1.96 2.28l-3.5-3A1.5 1.5 0 0 1 10 12V6.5a1.5 1.5 0 0 1 3 0Z""></path></svg>";

	// Busy
	public static string NormalBusy =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-busy);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M24 12a12 12 0 1 1-24 0 12 12 0 0 1 24 0Z""></path></svg>";

	// Do Not Disturb
	public static string NormalDoNotDisturb =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-dnd);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12 24a12 12 0 1 0 0-24 12 12 0 0 0 0 24ZM7.5 10.5h9a1.5 1.5 0 0 1 0 3h-9a1.5 1.5 0 0 1 0-3Z""></path></svg>";

	// Offline
	public static string NormalOffline =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-offline);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M16.06 7.94a1.5 1.5 0 0 1 0 2.12L14.12 12l1.94 1.94a1.5 1.5 0 0 1-2.12 2.12L12 14.12l-1.94 1.94a1.5 1.5 0 0 1-2.12-2.12L9.88 12l-1.94-1.94a1.5 1.5 0 1 1 2.12-2.12L12 9.88l1.94-1.94a1.5 1.5 0 0 1 2.12 0ZM0 12a12 12 0 1 1 24 0 12 12 0 0 1-24 0Zm12-9a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z""></path></svg>";

	// Out of Office
	public static string NormalOutOfOffice =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-oof);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12.56 9.06a1.5 1.5 0 0 0-2.12-2.12l-4 4a1.5 1.5 0 0 0 0 2.12l4 4a1.5 1.5 0 0 0 2.12-2.12l-1.44-1.44h5.38a1.5 1.5 0 1 0 0-3h-5.38l1.44-1.44ZM12 0a12 12 0 1 0 0 24 12 12 0 0 0 0-24ZM3 12a9 9 0 1 1 18 0 9 9 0 0 1-18 0Z""></path></svg>";

	// Unknown
	public static string NormalUnknown =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-unknown);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18ZM0 12a12 12 0 1 1 24 0 12 12 0 0 1-24 0Z""></path></svg>";

	//
	//Open(Out of Office)
	//

	// Available
	public static string OpenAvailable =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-available);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12 0a12 12 0 1 0 0 24 12 12 0 0 0 0-24ZM3 12a9 9 0 1 1 18 0 9 9 0 0 1-18 0Zm14.06-3.56a1.5 1.5 0 0 1 0 2.12l-5.5 5.5a1.5 1.5 0 0 1-2.12 0l-2-2a1.5 1.5 0 0 1 2.12-2.12l.94.94 4.44-4.44a1.5 1.5 0 0 1 2.12 0Z""></path></svg>";

	// Away
	public static string OpenAway =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-away);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M16.06 7.94a1.5 1.5 0 0 1 0 2.12L14.12 12l1.94 1.94a1.5 1.5 0 0 1-2.12 2.12L12 14.12l-1.94 1.94a1.5 1.5 0 0 1-2.12-2.12L9.88 12l-1.94-1.94a1.5 1.5 0 1 1 2.12-2.12L12 9.88l1.94-1.94a1.5 1.5 0 0 1 2.12 0ZM0 12a12 12 0 1 1 24 0 12 12 0 0 1-24 0Zm12-9a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z""></path></svg>";

	// Busy
	public static string OpenBusy =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-busy);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18ZM0 12a12 12 0 1 1 24 0 12 12 0 0 1-24 0Z""></path></svg>";

	// Do Not Disturb
	public static string OpenDoNotDisturb =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-dnd);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12 0a12 12 0 1 0 0 24 12 12 0 0 0 0-24ZM3 12a9 9 0 1 1 18 0 9 9 0 0 1-18 0Zm3 0c0-.83.67-1.5 1.5-1.5h9a1.5 1.5 0 0 1 0 3h-9A1.5 1.5 0 0 1 6 12Z""></path></svg>";

	// Offline
	public static string OpenOffline =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-offline);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M16.06 7.94a1.5 1.5 0 0 1 0 2.12L14.12 12l1.94 1.94a1.5 1.5 0 0 1-2.12 2.12L12 14.12l-1.94 1.94a1.5 1.5 0 0 1-2.12-2.12L9.88 12l-1.94-1.94a1.5 1.5 0 1 1 2.12-2.12L12 9.88l1.94-1.94a1.5 1.5 0 0 1 2.12 0ZM0 12a12 12 0 1 1 24 0 12 12 0 0 1-24 0Zm12-9a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z""></path></svg>";

	// Out of Office
	public static string OpenOutOfOffice =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-oof);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12.56 9.06a1.5 1.5 0 0 0-2.12-2.12l-4 4a1.5 1.5 0 0 0 0 2.12l4 4a1.5 1.5 0 0 0 2.12-2.12l-1.44-1.44h5.38a1.5 1.5 0 1 0 0-3h-5.38l1.44-1.44ZM12 0a12 12 0 1 0 0 24 12 12 0 0 0 0-24ZM3 12a9 9 0 1 1 18 0 9 9 0 0 1-18 0Z""></path></svg>";

	// Unknown
	public static string OpenUnknown =>
		@"<svg class=""status"" style=""width: {size}px; fill: var(--presence-unknown);"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true""><path d=""M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18ZM0 12a12 12 0 1 1 24 0 12 12 0 0 1-24 0Z""></path></svg>";
}