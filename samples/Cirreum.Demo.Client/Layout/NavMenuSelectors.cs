namespace Cirreum.Demo.Client.Layout;

public static class NavMenuSelectors {

	public const string NO_SCROLL_CONTAINER = "body";
	public const string NO_SCROLL_CLASS = "noscroll";
	public const string SHOW = "show";
	public const string SHOWING = "showing";
	public const string HIDING = "hiding";
	public const string HIDE = "d-none";

	public const string NavMenuOffCanvasBodyClass = "offcanvas-body";
	public const string NavMenuContainerClass = "nav-menu-container";
	public const string NavMenuClass = "nav-menu";

	/// <summary>
	/// $"{App.ElementId} .{NavMenuContainerClass}"
	/// </summary>
	public static readonly string NavMenuContainerSelector = $"{App.ElementId} .{NavMenuContainerClass}";

	/// <summary>
	/// $"{App.ElementId} .{NavMenuContainerClass} .{NavMenuOffCanvasBodyClass}"
	/// </summary>
	public static readonly string NavMenuOffCanvasBodySelector = $"{App.ElementId} .{NavMenuContainerClass} .{NavMenuOffCanvasBodyClass}";

	/// <summary>
	/// random element id
	/// </summary>
	public static readonly string NavMenuOffCanvasId = IdGenerator.Next;
	/// <summary>
	/// $"#{NavMenuOffCanvasId}"
	/// </summary>
	public static readonly string NavMenuOffCanvasSelector = $"#{NavMenuOffCanvasId}";

	/// <summary>
	/// random element id
	/// </summary>
	public static readonly string NavMenuOffCanvasLabelId = IdGenerator.Next;
	/// <summary>
	/// $"#{NavMenuOffCanvasLabelId}"
	/// </summary>
	public static readonly string NavMenuOffCanvasLabelSelector = $"#{NavMenuOffCanvasLabelId}";


	/// <summary>
	/// random element id
	/// </summary>
	public static readonly string NavMenuOffCanvasBackdropId = IdGenerator.Next;
	/// <summary>
	/// $"#{NavMenuOffCanvasBackdropId}"
	/// </summary>
	public static readonly string NavMenuOffCanvasBackdropSelector = $"#{NavMenuOffCanvasBackdropId}";

	/// <summary>
	/// $"{App.ElementId} .{NavMenuClass}"
	/// </summary>
	public static readonly string NavMenuSelector = $"{App.ElementId} .{NavMenuClass}";

	/// <summary>
	/// $"{App.ElementId} a.nav-link.active"
	/// </summary>
	public static readonly string ActiveNavLinkSelector = $"{App.ElementId} a.nav-link.active";

}
