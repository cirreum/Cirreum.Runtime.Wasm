namespace Cirreum.Components.Theme;

public class DefaultThemeModeIconProvider : IThemeModeIconProvider {

	public string ResolveModeIcon(ThemeMode mode) => mode switch {
		ThemeMode.Light => "bi-sun-fill",
		ThemeMode.Dark => "bi-moon-stars-fill",
		ThemeMode.Auto => "bi-circle-half",
		_ => "bi-question-circle"
	};

}