namespace Cirreum.Demo.Client;

public partial class App {

	/// <summary>
	/// The selector of the Blazor head section outlet within the HTML DOM.
	/// </summary>
	public const string HeadOutlet = "head::after";

	/// <summary>
	/// The selector of the Blazor App element within the HTML DOM.
	/// </summary>
	public const string ElementId = "#app";

	//public const string AppTitle = "Cirreum";

	//public const string NavMenuOffCanvasBodyClass = "offcanvas-body";
	//public const string NavMenuContainerClass = "nav-menu-container";
	//public const string NavMenuClass = "nav-menu";

	///// <summary>
	///// $"{ElementId} .{NavMenuContainerClass}"
	///// </summary>
	//public static readonly string NavMenuContainerSelector = $"{ElementId} .{NavMenuContainerClass}";

	///// <summary>
	///// $"{ElementId} .{NavMenuContainerClass} .{NavMenuOffCanvasBodyClass}"
	///// </summary>
	//public static readonly string NavMenuOffCanvasBodySelector = $"{ElementId} .{NavMenuContainerClass} .{NavMenuOffCanvasBodyClass}";

	///// <summary>
	///// random element id
	///// </summary>
	//public static readonly string NavMenuOffCanvasId = IdGenerator.Next;
	///// <summary>
	///// $"#{NavMenuOffCanvasId}"
	///// </summary>
	//public static readonly string NavMenuOffCanvasSelector = $"#{NavMenuOffCanvasId}";

	///// <summary>
	///// random element id
	///// </summary>
	//public static readonly string NavMenuOffCanvasLabelId = IdGenerator.Next;
	///// <summary>
	///// $"#{NavMenuOffCanvasLabelId}"
	///// </summary>
	//public static readonly string NavMenuOffCanvasLabelSelector = $"#{NavMenuOffCanvasLabelId}";


	///// <summary>
	///// random element id
	///// </summary>
	//public static readonly string NavMenuOffCanvasBackdropId = IdGenerator.Next;
	///// <summary>
	///// $"#{NavMenuOffCanvasBackdropId}"
	///// </summary>
	//public static readonly string NavMenuOffCanvasBackdropSelector = $"#{NavMenuOffCanvasBackdropId}";

	///// <summary>
	///// $"{ElementId} .{NavMenuClass}"
	///// </summary>
	//public static readonly string NavMenuSelector = $"{ElementId} .{NavMenuClass}";

	///// <summary>
	///// $"{ElementId} a.nav-link.active"
	///// </summary>
	//public static readonly string ActiveNavLinkSelector = $"{ElementId} a.nav-link.active";

	///// <summary>
	///// random element id
	///// </summary>
	//public static readonly string SessionStateId = IdGenerator.Next;

	//protected override void OnInitialized() {
	//	Console.WriteLine($"{nameof(App)} called using Session: {sessionManager.SessionId}");
	//}

	//private void OnUserContinued() {
	//	// Custom logic when user continues session
	//	Console.WriteLine("[APP] User chose to continue their session");
	//	Console.WriteLine($"[APP] Time remaining after continue: {sessionManager.TimeRemaining}");
	//	Console.WriteLine($"[APP] Current stage after continue: {sessionManager.CurrentStage?.Name}");
	//}

}