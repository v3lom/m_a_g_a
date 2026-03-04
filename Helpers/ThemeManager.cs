using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace M_A_G_A.Helpers
{
    /// <summary>
    /// Applies dark or light theme by replacing color/brush resources in
    /// Application.Current.Resources.  Call before any Window is created
    /// so that StaticResource references inside styles pick up the correct
    /// values at first use.
    /// </summary>
    public static class ThemeManager
    {
        private static readonly Dictionary<string, Color> Dark = new Dictionary<string, Color>
        {
            ["BgColor"]            = Color.FromRgb(10,  10,  10),
            ["SurfaceColor"]       = Color.FromRgb(20,  20,  20),
            ["Surface2Color"]      = Color.FromRgb(30,  30,  30),
            ["Surface3Color"]      = Color.FromRgb(42,  42,  42),
            ["AccentColor"]        = Colors.White,
            ["TextColor"]          = Colors.White,
            ["TextSecondaryColor"] = Color.FromRgb(136, 136, 136),
            ["OnlineColor"]        = Color.FromRgb(0,   230, 118),
            ["BorderColor"]        = Color.FromRgb(42,  42,  42),
        };

        private static readonly Dictionary<string, Color> Light = new Dictionary<string, Color>
        {
            ["BgColor"]            = Color.FromRgb(242, 242, 242),
            ["SurfaceColor"]       = Colors.White,
            ["Surface2Color"]      = Color.FromRgb(238, 238, 238),
            ["Surface3Color"]      = Color.FromRgb(224, 224, 224),
            ["AccentColor"]        = Color.FromRgb(30,  30,  30),
            ["TextColor"]          = Color.FromRgb(15,  15,  15),
            ["TextSecondaryColor"] = Color.FromRgb(100, 100, 100),
            ["OnlineColor"]        = Color.FromRgb(0,   180,  80),
            ["BorderColor"]        = Color.FromRgb(210, 210, 210),
        };

        private static readonly Dictionary<string, string> ColorToBrush = new Dictionary<string, string>
        {
            ["BgColor"]            = "BgBrush",
            ["SurfaceColor"]       = "SurfaceBrush",
            ["Surface2Color"]      = "Surface2Brush",
            ["Surface3Color"]      = "Surface3Brush",
            ["AccentColor"]        = "AccentBrush",
            ["TextColor"]          = "TextBrush",
            ["TextSecondaryColor"] = "TextSecBrush",
            ["OnlineColor"]        = "OnlineBrush",
            ["BorderColor"]        = "BorderBrush2",
        };

        public static void Apply(bool isLight)
        {
            var theme = isLight ? Light : Dark;
            var res   = Application.Current.Resources;
            foreach (var kv in theme)
            {
                res[kv.Key] = kv.Value;
                if (ColorToBrush.TryGetValue(kv.Key, out var brushKey))
                    res[brushKey] = new SolidColorBrush(kv.Value);
            }
        }
    }
}
