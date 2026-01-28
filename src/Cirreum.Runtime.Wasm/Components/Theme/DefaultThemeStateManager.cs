namespace Cirreum.Components.Theme;

public sealed class DefaultThemeStateManager(
	IThemeState themeState,
	IJSAppModule js
) : IThemeStateManager {

	public void SetMode(ThemeMode mode) {
		using var scope = themeState.CreateNotificationScope();

		var modeString = mode.ToShortName(); // "light", "dark", "auto"
		this.SetStoredMode(modeString);

		var appliedMode = modeString;
		if (mode == ThemeMode.Auto) {
			themeState.SetMode(modeString);
			appliedMode = js.GetSystemThemeMode(); // "light" or "dark"
		} else {
			themeState.SetMode(modeString);
		}

		js.SetElementAttribute("html", "data-bs-theme", appliedMode);
		themeState.SetAppliedMode(appliedMode);
	}

	public void SetScheme(ColorScheme scheme) {
		var newSchemeId = scheme.Id;
		if (themeState.Theme == newSchemeId) {
			return;
		}

		using var scope = themeState.CreateNotificationScope();

		this.SetStoredScheme(newSchemeId);
		js.SetElementAttribute("html", "data-color-scheme", newSchemeId);

		// Get current and new paths
		var currentScheme = ColorSchemes.GetOrDefault(themeState.Theme);
		var currentThemePath = ThemeResourcePaths.GetSchemePath(currentScheme);
		var newThemePath = ThemeResourcePaths.GetSchemePath(scheme);

		// Update our UI
		js.ReplaceHeadLink(currentThemePath, newThemePath);

		// Update our state
		themeState.SetTheme(newSchemeId);
	}

	private void SetStoredMode(string mode) =>
		js.InvokeVoid("localStorage.setItem", StorageKeys.ModeKey, mode);

	private void SetStoredScheme(string schemeId) =>
		js.InvokeVoid("localStorage.setItem", StorageKeys.SchemeKey, schemeId);

}