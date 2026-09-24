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
            res["AccentBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#26354D"));

            res["TextHighBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["TextMediumBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

            // Brand Accents
            res["ElectricCyanBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF"));

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

            res["WarningBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A1E16"));
            res["WarningBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            res["WarningBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));

            // Modals
            res["ModalSurfaceBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F131E"));
            res["ModalBackdropBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#55050A14"));

            // Hero Gradients
            var heroGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00E5FF"), 0.0));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0284C7"), 0.5));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2563EB"), 1.0));
            res["BrandHeroGradientBrush"] = heroGrad;

            var heroGradTop = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00E5FF"), 0.0));
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0284C7"), 0.5));
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2563EB"), 1.0));
            res["BrandHeroGradientBrushTop"] = heroGradTop;

            var heroBtn = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00C7E5"), 0.0));
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0284C7"), 0.5));
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0369A1"), 1.0));
            res["HeroActionBtnBrush"] = heroBtn;

            var heroBtnPressed = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00A3BC"), 0.0));
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0369A1"), 0.5));
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#075985"), 1.0));
            res["HeroActionBtnPressedBrush"] = heroBtnPressed;

            var outerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1A2234"), 0.0));
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#111624"), 1.0));
            res["DoubleBezelOuterBrush"] = outerGrad;

            var innerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#121622"), 0.0));
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0C0F18"), 1.0));
            res["DoubleBezelInnerBrush"] = innerGrad;

            var glassActive = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            glassActive.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2800E5FF"), 0.0));
            glassActive.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#140284C7"), 0.6));
            glassActive.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0D2563EB"), 1.0));
            res["GlassSurfaceActiveBrush"] = glassActive;
        }
        else
        {
            // ============================================
            // PRO EYE-COMFORT PORCELAIN & SLATE PALETTE
            // ============================================
            // Calm, glare-free canvas: Slate 100 provides gentle contrast so pure white cards float effortlessly
            res["CanvasDarkBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            res["HeaderDockBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            res["SurfaceCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            res["SurfaceCardHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["SurfaceCardSelectedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F9FF"));
            res["SurfaceSubCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["HairlineBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
            res["AccentBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));

            // Deep, crisp Slate typography for effortless readability without eye strain
            res["TextHighBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            res["TextMediumBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

            // Executive Ocean Azure accent (replaces blinding neon cyan in light mode)
            res["ElectricCyanBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));

            // Buttons & Controls (Clean Slate 50 with crisp borders)
            res["PillButtonBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["PillButtonHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            res["PillButtonBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));

            // Switches & Progress
            res["TrackBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
            res["TrackThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            res["ScrollThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
            res["ScrollThumbHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));

            // Badges (Crisp, high-contrast, WCAG AA compliant)
            res["AdminBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
            res["AdminBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7F3D0"));
            res["AdminBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#047857"));

            res["SafetyBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
            res["SafetyBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A7F3D0"));
            res["SafetyBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#047857"));

            res["WarningBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFBEB"));
            res["WarningBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDE68A"));
            res["WarningBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B45309"));

            // Modals
            res["ModalSurfaceBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            res["ModalBackdropBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#600F172A"));

            // Hero Gradients (Refined Ocean Azure -> Royal Blue)
            var heroGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0284C7"), 0.0));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2563EB"), 0.5));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1D4ED8"), 1.0));
            res["BrandHeroGradientBrush"] = heroGrad;

            var heroGradTop = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0284C7"), 0.0));
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2563EB"), 0.5));
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1D4ED8"), 1.0));
            res["BrandHeroGradientBrushTop"] = heroGradTop;

            var heroBtn = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0284C7"), 0.0));
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2563EB"), 0.5));
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1D4ED8"), 1.0));
            res["HeroActionBtnBrush"] = heroBtn;

            var heroBtnPressed = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0369A1"), 0.0));
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1D4ED8"), 0.5));
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1E40AF"), 1.0));
            res["HeroActionBtnPressedBrush"] = heroBtnPressed;

            var outerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F1F5F9"), 0.0));
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#E2E8F0"), 1.0));
            res["DoubleBezelOuterBrush"] = outerGrad;

            var innerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFFFFF"), 0.0));
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#F8FAFC"), 1.0));
            res["DoubleBezelInnerBrush"] = innerGrad;

            var glassActiveLight = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            glassActiveLight.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1E0284C7"), 0.0));
            glassActiveLight.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0F2563EB"), 1.0));
            res["GlassSurfaceActiveBrush"] = glassActiveLight;
        }
    }
}
