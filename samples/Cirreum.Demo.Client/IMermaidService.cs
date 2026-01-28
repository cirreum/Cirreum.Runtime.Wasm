namespace Cirreum.Demo.Client;

public interface IMermaidService {
	Task InitializeAsync();
	Task<string> RenderDiagramAsync(string diagramDefinition, string? theme = null);
	Task ClearDiagramAsync(string diagramId);
	Task<string> GetCurrentThemeAsync();
	bool IsInitialized { get; }

	/// <summary>
	/// Event raised when the Bootstrap theme changes.
	/// Multiple subscribers can listen to this event.
	/// </summary>
	event Action<string>? ThemeChanged;
}
