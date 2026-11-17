using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace TimeZoneCalc;

internal static class Theme
{
    public static readonly Brush Transparent = new SolidColorBrush(Colors.Transparent);

    // 目前實際套用的主題；MainWindow 在 ActualThemeChanged 時更新並重畫
    public static ElementTheme Actual { get; set; } = ElementTheme.Default;

    // Application.Current.Resources[key] 只會給出啟動當下主題的 brush，執行中切換深淺色後會是舊色，
    // 所以自己依 Actual 到各層 ThemeDictionaries 找
    public static Brush Get(string key)
    {
        var app = Application.Current;
        var dark = Actual == ElementTheme.Dark || (Actual == ElementTheme.Default && app.RequestedTheme == ApplicationTheme.Dark);
        return Find(app.Resources, key, dark ? "Dark" : "Light") as Brush ?? (Brush)app.Resources[key];
    }

    // 後面合併進來的字典優先；WinUI 的深色資源放在 "Default"，淺色放在 "Light"
    private static object? Find(ResourceDictionary dictionary, string key, string theme)
    {
        for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
            if (Find(dictionary.MergedDictionaries[i], key, theme) is { } merged)
                return merged;
        foreach (var name in theme == "Dark" ? new[] { "Dark", "Default" } : new[] { "Light", "Default" })
            if (dictionary.ThemeDictionaries.TryGetValue(name, out var themed)
                && themed is ResourceDictionary themeDictionary
                && themeDictionary.TryGetValue(key, out var value))
                return value;
        return null;
    }
}
