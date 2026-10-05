using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Styling;
using L2Toolkit.Settings;

namespace L2Toolkit.Utilities;

/// <summary>
/// App color theme. Dark is the default; the choice persists in the settings file.
/// Colors live in Themes/Colors.axaml as Theme* brushes for both variants.
/// </summary>
public static class AppTheme
{
    private const string SettingKey = "app_theme";
    private const string LightValue = "light";
    private const string DarkValue = "dark";

    public static ThemeVariant Saved =>
        AppDatabase.GetInstance().GetValue(SettingKey, DarkValue) == LightValue ? ThemeVariant.Light : ThemeVariant.Dark;

    public static void ApplySaved() => Application.Current!.RequestedThemeVariant = Saved;

    public static void Set(ThemeVariant variant)
    {
        Application.Current!.RequestedThemeVariant = variant;
        AppDatabase.GetInstance().UpdateValue(SettingKey, variant == ThemeVariant.Light ? LightValue : DarkValue);
    }

    /// <summary>
    /// Binding to a Theme* brush that follows theme changes, for controls built in code:
    /// <c>new Border { [!Border.BackgroundProperty] = AppTheme.Brush("ThemeSurfaceCard") }</c>.
    /// </summary>
    public static IBinding Brush(string key) => new DynamicResourceExtension(key);
}
