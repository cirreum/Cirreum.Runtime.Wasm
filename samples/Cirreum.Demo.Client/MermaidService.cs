namespace Cirreum.Demo.Client;

using Microsoft.JSInterop;

public class MermaidService(
	IJSRuntime jsRuntime
) : IMermaidService
  , IAsyncDisposable {

	private IJSObjectReference? _mermaidModule;
	private bool _isInitialized = false;
	private bool _isWatchingTheme = false;
	private DotNetObjectReference<MermaidService>? _dotNetRef;

	public bool IsInitialized => this._isInitialized;

	/// <inheritdoc />
	public event Action<string>? ThemeChanged;

	public async Task InitializeAsync() {
		if (this._isInitialized) {
			return;
		}
		this._isInitialized = true;

		this._mermaidModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/mermaid-service.js");
		await this._mermaidModule.InvokeVoidAsync("initializeMermaid");

		// Start watching theme changes automatically
		await this.StartWatchingThemeChangesAsync();
	}

	public async Task<string> RenderDiagramAsync(string diagramDefinition, string? theme = null) {
		if (!this._isInitialized) {
			await this.InitializeAsync();
		}

		if (this._mermaidModule == null) {
			throw new InvalidOperationException("Mermaid service not initialized");
		}

		return await this._mermaidModule.InvokeAsync<string>("renderDiagram", diagramDefinition, theme);
	}

	public async Task ClearDiagramAsync(string diagramId) {
		if (this._mermaidModule != null) {
			await this._mermaidModule.InvokeVoidAsync("clearDiagram", diagramId);
		}
	}

	public async Task<string> GetCurrentThemeAsync() {
		if (!this._isInitialized) {
			await this.InitializeAsync();
		}

		if (this._mermaidModule == null) {
			throw new InvalidOperationException("Mermaid service not initialized");
		}

		return await this._mermaidModule.InvokeAsync<string>("getCurrentTheme");
	}

	private async Task StartWatchingThemeChangesAsync() {
		if (this._isWatchingTheme || this._mermaidModule == null) {
			return;
		}

		this._dotNetRef = DotNetObjectReference.Create(this);
		await this._mermaidModule.InvokeVoidAsync("watchThemeChanges", this._dotNetRef);
		this._isWatchingTheme = true;
	}

	private async Task StopWatchingThemeChangesAsync() {
		if (this._mermaidModule != null && this._isWatchingTheme) {
			await this._mermaidModule.InvokeVoidAsync("stopWatchingThemeChanges");
		}
		this._dotNetRef?.Dispose();
		this._dotNetRef = null;
		this._isWatchingTheme = false;
	}

	[JSInvokable]
	public void OnThemeChanged(string newTheme) {
		// Raise event to all subscribers
		ThemeChanged?.Invoke(newTheme);
	}

	public async ValueTask DisposeAsync() {
		GC.SuppressFinalize(this);
		await this.StopWatchingThemeChangesAsync();
		if (this._mermaidModule != null) {
			await this._mermaidModule.DisposeAsync();
		}
	}

}
