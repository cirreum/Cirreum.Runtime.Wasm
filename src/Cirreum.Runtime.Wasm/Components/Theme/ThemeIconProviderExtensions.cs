namespace Cirreum.Components.Theme;

public static class ThemeIconProviderExtensions {

	public static string ResolveModeIcon(this IThemeModeIconProvider provider, string mode) =>
		Enum.TryParse<ThemeMode>(mode, ignoreCase: true, out var parsed)
			? provider.ResolveModeIcon(parsed)
			: provider.ResolveModeIcon(ThemeMode.Auto); // or default

}