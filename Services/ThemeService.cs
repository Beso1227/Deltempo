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
            // ============================================
            // LUXURY DARK OBSIDIAN PALETTE
            // ============================================
            res["CanvasDarkBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#090B10"));
            res["HeaderDockBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D1017"));
            res["SurfaceCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111520"));
            res["SurfaceCardHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#171D2C"));
            res["SurfaceCardSelectedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#141A28"));
            res["SurfaceSubCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#141926"));
            res["HairlineBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2538"));

            res["TextHighBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["TextMediumBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

            // Buttons & Controls
            res["PillButtonBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#151D2A"));
            res["PillButtonHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
            res["PillButtonBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#26354D"));

            // Switches & Progress
            res["TrackBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A2333"));
            res["TrackThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            res["ScrollThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A3448"));
            res["ScrollThumbHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#06B6D4"));

            // Badges
            res["AdminBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10241B"));
            res["AdminBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            res["AdminBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

            res["SafetyBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10241B"));
            res["SafetyBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            res["SafetyBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));

            // Modals
            res["ModalSurfaceBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F131E"));
            res["ModalBackdropBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EE05070B"));

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
            // ============================================
            // NORDIC PORCELAIN & FROST LIGHT PALETTE
            // ============================================
            res["CanvasDarkBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["HeaderDockBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            res["SurfaceCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            res["SurfaceCardHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            res["SurfaceCardSelectedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
            res["SurfaceSubCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            res["HairlineBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));

            res["TextHighBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            res["TextMediumBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));

            // Buttons & Controls (Clean Light Grey with Border)
            res["PillButtonBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            res["PillButtonHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
            res["PillButtonBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));

            // Switches & Progress
            res["TrackBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
            res["TrackThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            res["ScrollThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
            res["ScrollThumbHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));

            // Badges (Crisp Pastel Emerald on Light)
            res["AdminBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
            res["AdminBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6EE7B7"));
            res["AdminBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));

            res["SafetyBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
            res["SafetyBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7F3D0"));
            res["SafetyBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));

            // Modals
            res["ModalSurfaceBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            res["ModalBackdropBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#770F172A"));

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
