namespace Cirreum.Components.Theme;

using Microsoft.AspNetCore.Components;

public class StyleSheetSwitcher(
	IJSAppModule JSApp,
	IThemeState themeState
) : StateComponentBase {

	string priorMode = "";

	/// <summary>
	/// The Uri of the style sheet (*.css file) to load for the Light theme.
	/// </summary>
	[Parameter, EditorRequired]
	public string LightThemeHref { get; set; } = default!;

	/// <summary>
	/// The Uri of the style sheet (*.css file) to load for the Dark theme.
	/// </summary>
	[Parameter, EditorRequired]
	public string DarkThemeHref { get; set; } = default!;

	protected override void OnInitialized() {
		this.SubscribeToStateChanges<IThemeState>();
	}

	protected override void OnAfterRender(bool firstRender) {

		if (firstRender) {
			this.priorMode = themeState.Mode;
		}

		var currentMode = themeState.Mode;

		if (firstRender is false && this.priorMode == currentMode) {
			// only process if the mode has changed
			return;
		}

		if (currentMode == ThemeModeNames.Auto) {
			var systemThemeMode = JSApp.GetSystemThemeMode();
			if (this.priorMode.HasValue() && systemThemeMode != this.priorMode) {
				if (systemThemeMode == ThemeModeNames.Dark) {
					JSApp.ReplaceHeadLink(this.LightThemeHref, this.DarkThemeHref);
				} else {
					JSApp.ReplaceHeadLink(this.DarkThemeHref, this.LightThemeHref);
				}
			}
			this.priorMode = currentMode;
			return;
		}

		if (currentMode == ThemeModeNames.Light) {
			JSApp.ReplaceHeadLink(this.DarkThemeHref, this.LightThemeHref);
			this.priorMode = currentMode;
			return;
		}

		if (currentMode == ThemeModeNames.Dark) {
			JSApp.ReplaceHeadLink(this.LightThemeHref, this.DarkThemeHref);
			this.priorMode = currentMode;
			return;
		}

		base.OnAfterRender(firstRender);

	}

}