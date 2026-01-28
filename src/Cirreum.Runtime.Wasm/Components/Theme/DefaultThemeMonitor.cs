namespace Cirreum.Components.Theme;

using Cirreum;
using Cirreum.Components.Interop;
using Microsoft.JSInterop;
using System.Diagnostics.CodeAnalysis;

public sealed class DefaultThemeMonitor(
	IThemeState themeState,
	IThemeStateManager themeManager,
	IJSAppModule js
) : IThemeMonitor {

	private bool _initialized;

	public ValueTask InitializeAsync() {

		if (this._initialized) {
			return ValueTask.CompletedTask;
		}
		this._initialized = true;

		// Restore persisted preferences for Mode
		var storedMode = this.GetStoredMode();
		if (string.IsNullOrEmpty(storedMode)) {
			storedMode = ThemeModeNames.Auto;
		}
		// Apply restored preferences
		if (Enum.TryParse<ThemeMode>(storedMode, true, out var mode)) {
			themeManager.SetMode(mode);
		}

		// Restore persisted preferences for Theme
		var storedSchemeId = this.GetStoredScheme();
		if (string.IsNullOrEmpty(storedSchemeId)) {
			storedSchemeId = ColorSchemes.DefaultId;
		}
		var scheme = ColorSchemes.GetOrDefault(storedSchemeId);
		// we don't call themeManager as our loader
		// script should have already applied it.
		// so we just update our internal state
		themeState.SetTheme(scheme.Id);
		//themeManager.SetScheme(scheme);

		// Start monitoring system changes
		this.MonitorSystemModeChanges();

		return ValueTask.CompletedTask;
	}

	public void RefreshAppliedMode() {
		if (themeState.Mode == ThemeModeNames.Auto) {
			var systemTheme = js.GetSystemThemeMode();
			js.SetElementAttribute("html", "data-bs-theme", systemTheme);
			themeState.SetAppliedMode(systemTheme);
		}
	}

	private string GetStoredMode() =>
		(js.Invoke<string?>("localStorage.getItem", StorageKeys.ModeKey) ?? "").ToLowerInvariant();

	private string GetStoredScheme() =>
		(js.Invoke<string?>("localStorage.getItem", StorageKeys.SchemeKey) ?? "").ToLowerInvariant();

	private void MonitorSystemModeChanges() {
		var themeMonitor = new ModeMonitorRef((isDarkMode, storedMode) => {
			if (storedMode == ThemeModeNames.Auto) {
				js.SetElementAttribute("html", "data-bs-theme", isDarkMode ? ThemeModeNames.Dark : ThemeModeNames.Light);
			}
			return Task.CompletedTask;
		});

		var themeMonitorRef = DotNetObjectReference.Create(themeMonitor);
		js.MonitorSystemThemeMode(themeMonitorRef);
	}
	private record ModeMonitorRef : IThemeModeChangedRef {
		[DynamicDependency(nameof(ModeChanged))]
		public ModeMonitorRef(Func<bool, string, Task>? onThemeChanged) {
			this.OnThemeModeChanged = onThemeChanged;
		}

		public Func<bool, string, Task>? OnThemeModeChanged { get; }

		[JSInvokable(EventNames.ModeChangedEvent)]
		public async Task ModeChanged(bool isDarkMode, string storedMode) {
			if (this.OnThemeModeChanged is not null) {
				await this.OnThemeModeChanged.Invoke(isDarkMode, storedMode);
			}
		}
	}

}