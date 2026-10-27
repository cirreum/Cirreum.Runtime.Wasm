namespace Cirreum.Components.Interop;

using Cirreum.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

/// <summary>
/// This class provides access to convenient JavaScript functionality. The associated
/// JavaScript module is imported by <see cref="InitializeAsync"/>, which the runtime's
/// system initializer runs before any component renders.
/// </summary>
/// <remarks>
/// Registered as a singleton; in Blazor WebAssembly there is one scope per application.
/// </remarks>
sealed class JSAppModule : IJSAppModule, IBrowserCultureInterop, IDisposable {

	private static readonly string InteropUrl = "./_content/Cirreum.Components.WebAssembly/js/cirreum-interop.js";
	private readonly IJSInProcessRuntime JSInProcess;
	private readonly IJSRuntime JSRuntime;
	private IJSInProcessObjectReference? module;

	/// <summary>
	/// The imported module. Every module-scoped call goes through this so a call made before
	/// <see cref="InitializeAsync"/> fails with a stated cause rather than a null reference.
	/// </summary>
	private IJSInProcessObjectReference Module =>
		this.module ?? throw new InvalidOperationException(
			$"{nameof(JSAppModule)}.{nameof(InitializeAsync)} has not completed; the interop module is not loaded.");

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="js">The <see cref="IJSRuntime"/> instance.</param>
	public JSAppModule(IJSRuntime js) {
		ArgumentNullException.ThrowIfNull(js);
		this.JSRuntime = js;
		this.JSInProcess = (IJSInProcessRuntime)js;
	}

	public async ValueTask InitializeAsync() {
		this.module = await this.JSRuntime.InvokeAsync<IJSInProcessObjectReference>("import", InteropUrl);
	}

	#region Arbitrary

	public void InvokeVoid(string identifier, params object?[]? args) {
		this.JSInProcess.InvokeVoid(identifier, args);
	}
	public TResult Invoke<TResult>(string identifier, params object?[]? args) {
		return this.JSInProcess.Invoke<TResult>(identifier, args);
	}

	public async ValueTask InvokeVoidAsync(string identifier, params object?[]? args) {
		await this.JSRuntime.InvokeVoidAsync(identifier, args);
	}
	public async ValueTask InvokeVoidAsync(string identifier, CancellationToken token, params object?[]? args) {
		await this.JSRuntime.InvokeVoidAsync(identifier, token, args);
	}
	public async ValueTask<TResult> InvokeAsync<TResult>(string identifier, params object?[]? args) {
		return await this.JSRuntime.InvokeAsync<TResult>(identifier, args);
	}
	public async ValueTask<TResult> InvokeAsync<TResult>(string identifier, CancellationToken token, params object?[]? args) {
		return await this.JSRuntime.InvokeAsync<TResult>(identifier, token, args);
	}

	#endregion

	#region Browser Environment

	/// <inheritdoc />
	public DateTimeFormatOptions GetInternationalFormats() {
		return this.Module.Invoke<DateTimeFormatOptions>("getInternationalFormats");
	}

	/// <inheritdoc />
	public string GetCurrentLocalTime() {
		return this.Module.Invoke<string>("getCurrentLocalTime");
	}

	/// <inheritdoc />
	public string GetCurrentUtcTime() {
		return this.Module.Invoke<string>("getCurrentUtcTime");
	}

	/// <inheritdoc />
	public string IsDaylightSavingTime() {
		return this.Module.Invoke<string>("isDaylightSavingTime");
	}

	/// <inheritdoc />
	public bool HasTimeZoneSupport() {
		return this.Module.Invoke<bool>("hasTimeZoneSupport");
	}

	/// <inheritdoc />
	public bool HasOffsetSupport() {
		return this.Module.Invoke<bool>("hasOffsetSupport");
	}

	/// <inheritdoc />
	public string GetUserAgent() {
		return this.Module.Invoke<string>("getUserAgent");
	}

	#endregion

	#region Cirreum

	/// <inheritdoc />
	public string GetAppName() {
		return this.Module.Invoke<string>("getAppName");
	}

	/// <inheritdoc />
	public string? GetAssemblyName() {
		return this.Module.Invoke<string?>("getAssemblyName");
	}

	/// <inheritdoc />
	public string GetCurrentTheme() {
		return this.Module.Invoke<string>("getCurrentTheme");
	}

	/// <inheritdoc />
	public string[] GetValidThemes() {
		return this.Module.Invoke<string[]>("getValidThemes");
	}

	#endregion

	#region Media Queries

	/// <inheritdoc />
	public bool IsStandAlone() {
		return this.Module.Invoke<bool>("isStandAlone");
	}

	/// <inheritdoc />
	public string GetSystemThemeMode() {
		return this.Module.Invoke<string>("getSystemThemeMode");
	}

	/// <inheritdoc />
	public void MonitorSystemThemeMode<T>(DotNetObjectReference<T> dotnetObjRef) where T : class, IThemeModeChangedRef {
		this.Module.InvokeVoid("monitorSystemThemeMode", dotnetObjRef);
	}

	/// <inheritdoc />
	public void RemoveSystemThemeModeMonitor() {
		this.Module.InvokeVoid("removeSystemThemeModeMonitor");
	}

	/// <inheritdoc />
	public bool GetCurrentBreakPoint(string minBreakpointSize) {
		return this.Module.Invoke<bool>("getCurrentBreakPoint", minBreakpointSize);
	}

	/// <inheritdoc />
	public void MonitorBreakpoint<T>(DotNetObjectReference<T> dotnetObjRef, string minBreakpointSize) where T : class {
		//OnBreakpointChange
		this.Module.InvokeVoid("monitorBreakpointChanges", dotnetObjRef, minBreakpointSize);
	}

	/// <inheritdoc />
	public void RemoveBreakpointMonitor(string minBreakpointSize) {
		this.Module.InvokeVoid("removeBreakpointMonitor", minBreakpointSize);
	}

	#endregion

	#region Head

	/// <inheritdoc />
	public void IncludeStyleSheet(string href, string? integrity = null, string? title = null, bool? disabled = null) {
		this.Module.InvokeVoid("includeStyleSheet", href, integrity, title, disabled);
	}

	/// <inheritdoc />
	public void ReplaceHeadLink(string oldHref, string newHref) {
		this.Module.InvokeVoid("replaceHeadLink", oldHref, newHref);
	}

	#endregion

	#region Elements

	public void FocusElement(ElementReference element, bool preventScroll = false) {
		this.Module.InvokeVoid("focusElement", element, preventScroll);
	}
	public void FocusElement(string element, bool preventScroll = false) {
		this.Module.InvokeVoid("focusElement", element, preventScroll);
	}
	public void FocusFirstElement(ElementReference container, bool preventScroll = false) {
		this.Module.InvokeVoid("focusFirstElement", container, preventScroll);
	}
	public void FocusFirstElement(string container, bool preventScroll = false) {
		this.Module.InvokeVoid("focusFirstElement", container, preventScroll);
	}
	public void FocusLastElement(ElementReference container, bool preventScroll = false) {
		this.Module.InvokeVoid("focusLastElement", container, preventScroll);
	}
	public void FocusLastElement(string container, bool preventScroll = false) {
		this.Module.InvokeVoid("focusLastElement", container, preventScroll);
	}
	public void FocusNextElement(bool reverse, ElementReference element, bool preventScroll = false) {
		this.Module.InvokeVoid("focusNextElement", reverse, element, preventScroll);
	}
	public void FocusNextElement(bool reverse, bool preventScroll, string? elementId = null) {
		this.Module.InvokeVoid("focusNextElement", reverse, elementId, preventScroll);
	}

	public bool IsVerticalScrollbarVisible(string selector) {
		return this.Module.Invoke<bool>("isVerticalScrollbarVisible", selector);
	}
	public bool IsHorizontalScrollbarVisible(string selector) {
		return this.Module.Invoke<bool>("isHorizontalScrollbarVisible", selector);
	}

	public void SetElementClassIfScrollbar(string selector, bool vertical, params string[] classes) {
		this.Module.InvokeVoid("addElementClassIfScrollbar", selector, vertical, classes);
	}

	public bool IsChildOf(ElementReference element, string selector) {
		return this.Module.Invoke<bool>("isChildOf", element, selector);
	}

	public void SetCheckBoxIndeterminate(ElementReference element, bool value) {
		this.Module.InvokeVoid("setCheckBoxIndeterminate", element, value);
	}
	public void SetCheckBoxIndeterminate(string selector, bool value) {
		this.Module.InvokeVoid("setCheckBoxIndeterminate", selector, value);
	}

	public void SetElementAttribute(ElementReference element, string property, object value) {
		this.Module.InvokeVoid("setElementAttribute", element, property, value);
	}
	public void SetElementAttribute(string selector, string property, object value) {
		this.Module.InvokeVoid("setElementAttribute", selector, property, value);
	}
	public void SetElementsAttribute(string selector, string property, object value) {
		this.Module.InvokeVoid("setElementsAttribute", selector, property, value);
	}

	public string GetElementAttribute(ElementReference element, string property) {
		return this.Module.Invoke<string>("getElementAttribute", element, property);
	}
	public string GetElementAttribute(string selector, string property) {
		return this.Module.Invoke<string>("getElementAttribute", selector, property);
	}

	public string GetElementText(ElementReference element, bool tryFirstChild = false) {
		return this.Module.Invoke<string>("getElementText", element, tryFirstChild);
	}
	public string GetElementText(string selector, bool tryFirstChild = false) {
		return this.Module.Invoke<string>("getElementText", selector, tryFirstChild);
	}

	public string GetElementTextContent(ElementReference element, bool tryFirstChild = false) {
		return this.Module.Invoke<string>("getElementTextContent", element, tryFirstChild);
	}
	public string GetElementTextContent(string selector, bool tryFirstChild = false) {
		return this.Module.Invoke<string>("getElementTextContent", selector, tryFirstChild);
	}

	public string GetElementId(ElementReference element) {
		return this.Module.Invoke<string>("getElementId", element);
	}

	public bool ParentContainsClass(ElementReference child, string token) {
		return this.Module.Invoke<bool>("parentContainsClass", child, token);
	}
	public bool ParentContainsClass(string selector, string token) {
		return this.Module.Invoke<bool>("parentContainsClass", selector, token);
	}

	public bool ElementContainsClass(ElementReference element, string token) {
		return this.Module.Invoke<bool>("elementContainsClass", element, token);
	}
	public bool ElementContainsClass(string selector, string token) {
		return this.Module.Invoke<bool>("elementContainsClass", selector, token);
	}

	public void AddElementClass(ElementReference element, string token) {
		this.AddElementClassesPrivate(element, token);
	}
	public void AddElementClass(string selector, string token) {
		this.AddElementClassesPrivate(selector, token);
	}
	public void AddElementClasses(ElementReference element, params string[] tokens) {
		if (tokens != null && tokens.Length > 0) {
			// If tokens is a single array, flatten it
			var flattenedTokens = string.Join(" ", tokens.SelectMany(t => t.Split(separator, StringSplitOptions.RemoveEmptyEntries)));
			this.AddElementClassesPrivate(element, flattenedTokens);
		}
	}
	public void AddElementClasses(string selector, params string[] tokens) {
		if (tokens != null && tokens.Length > 0) {
			// If tokens is a single array, flatten it
			var flattenedTokens = string.Join(" ", tokens.SelectMany(t => t.Split(separator, StringSplitOptions.RemoveEmptyEntries)));
			this.AddElementClassesPrivate(selector, flattenedTokens);
		}
	}
	private void AddElementClassesPrivate(object obj, string classString) {
		this.Module.InvokeVoid("addElementClasses", obj, classString);
	}

	public void RemoveElementClass(ElementReference element, string token) {
		this.RemoveElementClassesPrivate(element, [token]);
	}
	public void RemoveElementClass(string selector, string token) {
		this.RemoveElementClassesPrivate(selector, [token]);
	}
	public void RemoveElementClasses(ElementReference element, string[] tokens) {
		if (tokens != null && tokens.Length > 0) {
			this.RemoveElementClassesPrivate(element, tokens);
		}
	}
	public void RemoveElementClasses(string selector, string[] tokens) {
		if (tokens != null && tokens.Length > 0) {
			this.RemoveElementClassesPrivate(selector, tokens);
		}
	}
	private void RemoveElementClassesPrivate(object obj, string[] tokens) {
		this.Module.InvokeVoid("removeElementClasses", obj, tokens);
	}

	public bool ToggleElementClass(
		ElementReference element,
		string token,
		bool? force = null) {
		return this.Module.Invoke<bool>("toggleElementClass", element, token, force);
	}
	public bool ToggleElementClass(string selector, string token, bool? force = null) {
		return this.Module.Invoke<bool>("toggleElementClass", selector, token, force);
	}

	public void SetElementClass(ElementReference element, string value) {
		this.Module.InvokeVoid("setElementClass", element, value);
	}
	public void SetElementClass(string selector, string value) {
		this.Module.InvokeVoid("setElementClass", selector, value);
	}

	public bool SwapElementClass(ElementReference element, string token, string newToken) {
		return this.Module.Invoke<bool>("swapElementClass", element, token, newToken);
	}
	public bool SwapElementClass(string selector, string token, string newToken) {
		return this.Module.Invoke<bool>("swapElementClass", selector, token, newToken);
	}

	public void SetElementStyleProperty(string elementId, string property, string value)
		=> this.Module.InvokeVoid("setElementStyleProperty", elementId, property, value);

	public void ScrollElementIntoView(
		string selector,
		ScrollBehavior behavior = ScrollBehavior.Smooth,
		ScrollLogicalPosition block = ScrollLogicalPosition.Nearest,
		ScrollLogicalPosition inline = ScrollLogicalPosition.End) {
		var options = new {
			behavior = behavior.ToName(),
			block = block.ToName(),
			inline = inline.ToName()
		};
		this.Module.InvokeVoid("scrollIntoView", selector, options);
	}

	public void SetElementHeight(string selector, string height) {
		this.Module.InvokeVoid("setElementHeight", selector, height);
	}
	public void SetElementHeight(ElementReference element, string height) {
		this.Module.InvokeVoid("setElementHeight", element, height);
	}

	public void SetElementMaxHeight(string selector, string maxHeight) {
		this.Module.InvokeVoid("setElementMaxHeight", selector, maxHeight);
	}
	public void SetElementMaxHeight(ElementReference element, string maxHeight) {
		this.Module.InvokeVoid("setElementMaxHeight", element, maxHeight);
	}

	public void SetElementMaxHeightFromScrollHeight(string selector) {
		this.Module.InvokeVoid("setElementMaxHeightFromScrollHeight", selector);
	}
	public void SetElementMaxHeightFromScrollHeight(ElementReference element) {
		this.Module.InvokeVoid("setElementMaxHeightFromScrollHeight", element);
	}

	public int GetElementScrollHeight(string selector) {
		return this.Module.Invoke<int>("getElementScrollHeight", selector);
	}
	public int GetElementScrollHeight(ElementReference element) {
		return this.Module.Invoke<int>("getElementScrollHeight", element);
	}
	public int GetElementScrollWidth(string selector) {
		return this.Module.Invoke<int>("getElementScrollWidth", selector);
	}
	public int GetElementScrollWidth(ElementReference element) {
		return this.Module.Invoke<int>("getElementScrollWidth", element);
	}

	public int GetElementScrollLeft(string selector) {
		return this.Module.Invoke<int>("getElementScrollLeft", selector);
	}
	public int GetElementScrollLeft(ElementReference element) {
		return this.Module.Invoke<int>("getElementScrollLeft", element);
	}

	public void SetElementScrollLeft(string selector, int value) {
		this.Module.InvokeVoid("setElementScrollLeft", selector, value);
	}
	public void SetElementScrollLeft(ElementReference element, int value) {
		this.Module.InvokeVoid("setElementScrollLeft", element, value);
	}

	public int GetElementScrollTop(string selector) {
		return this.Module.Invoke<int>("getElementScrollTop", selector);
	}
	public int GetElementScrollTop(ElementReference element) {
		return this.Module.Invoke<int>("getElementScrollTop", element);
	}

	public void SetElementScrollTop(string selector, int value) {
		this.Module.InvokeVoid("setElementScrollTop", selector, value);
	}
	public void SetElementScrollTop(ElementReference element, int value) {
		this.Module.InvokeVoid("setElementScrollTop", element, value);
	}

	public DomRect? GetAccurateBoundingClientRect(ElementReference element) =>
		this.Module.Invoke<DomRect?>("getAccurateBoundingClientRect", element);

	public DomRect? GetAccurateBoundingClientRect(string selector) =>
		this.Module.Invoke<DomRect?>("getAccurateBoundingClientRect", selector);

	public DomRect? GetBoundingClientRect(string selector) {
		return this.Module.Invoke<DomRect?>("getBoundingClientRect", selector);
	}
	public DomRect? GetBoundingClientRect(ElementReference element) {
		return this.Module.Invoke<DomRect?>("getBoundingClientRect", element);
	}

	public ElementCoordinates GetElementCoordinates(string selector) {
		return this.Module.Invoke<ElementCoordinates>("getElementCoordinates", selector);
	}
	public ElementCoordinates GetElementCoordinates(ElementReference element) {
		return this.Module.Invoke<ElementCoordinates>("getElementCoordinates", element);
	}

	public ElementDimensions GetElementDimensions(ElementReference element) {
		return this.Module.Invoke<ElementDimensions>("getElementDimensions", element);
	}
	public ElementDimensions GetElementDimensions(string selector) {
		return this.Module.Invoke<ElementDimensions>("getElementDimensions", selector);
	}

	public ElementDimensions GetViewportDimensions() {
		return this.Module.Invoke<ElementDimensions>("getViewPortDimensions");
	}
	public Coordinates GetScrollPosition() {
		return this.Module.Invoke<Coordinates>("getScrollPosition");
	}

	public double GetFooterHeight() {
		// offsetHeight plus parsed margins: a rem-based margin yields a fraction.
		return this.Module.Invoke<double>("getFooterHeight");
	}

	#endregion

	#region PopperJS

	private const string POPPER_SHOW_METHOD = "showPopper";
	private const string POPPER_FORCEUPDATE_METHOD = "updatePopper";
	private const string POPPER_DESTROY_METHOD = "destroyPopper";
	private const string POPPER_APPLYSTYLES_MODIFIER = "applyStyles";
	private const string POPPER_MISSING_HOST_ELEMENT = "Missing required host element reference.";
	private const string POPPER_MISSING_POPPER_ELEMENT = "Missing required droppable element reference.";
	internal static readonly char[] separator = [' '];

	public void ForceUpdatePopperJS(string id) {
		this.Module.InvokeVoid(POPPER_FORCEUPDATE_METHOD, id);
	}
	public void ClosePopperJS(string id) {
		this.Module.InvokeVoid(POPPER_DESTROY_METHOD, id);
	}
	public string ShowPopperJS(PopperJSOptions options) {

		var host = options.TriggerElement.Context == null
			? throw new InvalidOperationException(POPPER_MISSING_HOST_ELEMENT)
			: options.TriggerElement;

		var popper = options.PopperElement.Context == null
			? throw new InvalidOperationException(POPPER_MISSING_POPPER_ELEMENT)
			: options.PopperElement;

		if (options.DisplayStatic) {
			options.Modifiers.Add(new PopperModifer(
				name: POPPER_APPLYSTYLES_MODIFIER,
				enabled: false
			));
		}

		var config = new {
			placement = options.Placement.ToName(),
			modifiers = options.Modifiers.ToArray()
		};

		var uniqueId = IdGenerator.Next;
		this.Module.InvokeVoid(POPPER_SHOW_METHOD, uniqueId, host, popper, config);

		return uniqueId;

	}

	#endregion

	#region MouseButtonListener

	public void AddMouseButtonListener<T>(
		string elementIdOrSelector,
		DotNetObjectReference<T> callback,
		int delay,
		MouseButton button) where T : class {
		this.Module.InvokeVoid(
			"addMouseButtonListener",
			elementIdOrSelector,
			callback,
			delay,
			button.GetButtonNumber());
	}
	public void AddMouseButtonListener<T>(
		ElementReference element,
		DotNetObjectReference<T> callback,
		int delay,
		MouseButton button) where T : class {
		this.Module.InvokeVoid(
			"addMouseButtonListener",
			element,
			callback,
			delay,
			button.GetButtonNumber());
	}
	public void RemoveMouseButtonListener(string elementIdOrSelector) {
		this.Module.InvokeVoid("removeMouseButtonListener", elementIdOrSelector);
	}
	public void RemoveMouseButtonListener(ElementReference element) {
		this.Module.InvokeVoid("removeMouseButtonListener", element);
	}

	#endregion

	public void Dispose() {
		this.module?.Dispose();
	}

}