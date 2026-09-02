using System.Windows;
using System.Windows.Media;

namespace WinTempCleaner.Services;

public static class ThemeService
{
    public static bool IsDarkMode { get; set; } = true;

    public static void SetTheme(bool isDark)
    {
        IsDarkMode = isDark;
        var res = Application.Current.Resources;

        if (isDark)
        {
            // Dark Mode Palette
            res["CanvasDarkBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#090B10"));
            res["SurfaceCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111520"));
            res["SurfaceCardHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#171D2C"));
            res["SurfaceCardSelectedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#141A28"));
            res["HairlineBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2538"));

            res["TextHighBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["TextMediumBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#526079"));

            var outerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1A2234"), 0.0));
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#111624"), 1.0));
            res["DoubleBezelOuterBrush"] = outerGrad;

            var innerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#121622"), 0.0));
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0C0F18"), 1.0));
            res["DoubleBezelInnerBrush"] = innerGrad;
        }
        else
        {
            // Light Mode (Nordic Frost Porcelain)
            res["CanvasDarkBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F6FB"));
            res["SurfaceCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            res["SurfaceCardHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            res["SurfaceCardSelectedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F0FE"));
            res["HairlineBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));

            res["TextHighBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            res["TextMediumBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8293A9"));

            var outerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#E2E8F0"), 0.0));
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#CBD5E1"), 1.0));
            res["DoubleBezelOuterBrush"] = outerGrad;

            var innerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFFFFF"), 0.0));
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F8FAFC"), 1.0));
            res["DoubleBezelInnerBrush"] = innerGrad;
        }
    }
}
