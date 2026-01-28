namespace Cirreum.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

/// <summary>
/// A base page component that provides common application services for Blazor pages.
/// Extends <see cref="ComponentBase"/> with automatic injection of frequently used services.
/// </summary>
/// <remarks>
/// <para>This base class provides a foundation for application pages by automatically injecting 
/// and providing access to commonly needed services. Use this when you need access to logging, 
/// user context, navigation, and communication services without state management overhead.</para>
/// <para>Automatically injects and provides access to the following services:</para>
/// <list type="bullet">
///     <item>
///         <description><see cref="ILogger{TCategoryName}"/> as <c>Logger</c> - Logging with automatic category naming</description>
///     </item>
///     <item>
///         <description><see cref="IUserState"/> as <c>CurrentUser</c> - Current user context and authentication</description>
///     </item>
///     <item>
///         <description><see cref="IJSAppModule"/> as <c>JSApp</c> - JavaScript interop services</description>
///     </item>
///     <item>
///         <description><see cref="IDialogService"/> as <c>Dialogr</c> - Modal dialog management</description>
///     </item>
///     <item>
///         <description><see cref="IToastService"/> as <c>Toastr</c> - Toast notification display</description>
///     </item>
/// </list>
/// <para>
/// For pages that require state management capabilities, inherit from <see cref="StatePageBase"/> instead.
/// For dynamic state management with automatic persistence, consider <see cref="SessionStatePage"/>, 
/// <see cref="LocalStatePage"/>, or <see cref="MemoryStatePage"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// @page "/mypage"
/// @inherits PageBase
/// 
/// &lt;h1&gt;Welcome @CurrentUser.Name&lt;/h1&gt;
/// &lt;button @onclick="ShowMessage"&gt;Click me&lt;/button&gt;
/// 
/// @code {
///     private async Task ShowMessage() {
///         Logger.LogInformation("User clicked button");
///         await Toastr.ShowSuccess("Hello from the page!");
///     }
/// }
/// </code>
/// </example>
public abstract class PageBase : ComponentBase, IDisposable {

	[Inject]
	private ILoggerFactory LogFactory { get; set; } = default!;
	private ILogger? _logger;
	/// <summary>
	/// An <see cref="ILogger{TCategoryName}"/>.
	/// </summary>
	/// <remarks>
	/// The category is derived from the <c>Type</c> of this page instance.
	/// </remarks>
	protected ILogger Logger => this._logger ??= this.LogFactory.CreateLogger(this.GetType());

	[Inject]
	private IUserState? PrivateUserState { get; set; }

	/// <summary>
	/// Gets the Current user.
	/// </summary>
	protected IUserState CurrentUser => this.PrivateUserState ?? throw new InvalidOperationException("User state not initialized");

	/// <summary>
	/// The <see cref="IJSAppModule"/> service.
	/// </summary>
	[Inject]
	protected IJSAppModule JSApp { get; set; } = default!;

	/// <summary>
	/// The <see cref="IConductor"/> service.
	/// </summary>
	[Inject]
	protected IConductor Conductor { get; set; } = default!;

	/// <summary>
	/// The <see cref="IDialogService"/> service.
	/// </summary>
	[Inject]
	protected IDialogService Dialogr { get; set; } = default!;

	/// <summary>
	/// The <see cref="IToastService"/> service.
	/// </summary>
	[Inject]
	protected IToastService Toastr { get; set; } = default!;

	/// <inheritdoc/>
	public void Dispose() {
		this.IsDisposing = true;
		this.Dispose(disposing: true);
	}

	protected bool IsDisposing { get; private set; }
	protected virtual void Dispose(bool disposing) {

	}

}