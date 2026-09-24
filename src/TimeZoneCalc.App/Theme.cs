using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace TimeZoneCalc;

internal static class Theme
{
    public static readonly Brush Transparent = new SolidColorBrush(Colors.Transparent);

    public static Brush Get(string key) => (Brush)Application.Current.Resources[key];
}
