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
            // LUXURY OBSIDIAN & CYBER MINT PALETTE
            // ============================================
            res["CanvasDarkBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#06080B"));
            res["HeaderDockBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0A0E13"));
            res["SurfaceCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0E1318"));
            res["SurfaceCardHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#131B22"));
            res["SurfaceCardSelectedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#101820"));
            res["SurfaceSubCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0A0F14"));
            res["HairlineBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16212B"));
            res["AccentBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A3B35"));

            res["TextHighBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["TextMediumBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

            // Brand Accents
            res["ElectricCyanBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F2B0"));
            res["AccentBlueBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0DD3BA"));
            res["AccentIndigoBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#06B6D4"));
            res["SuccessGlowBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F2B0"));
            res["DestructiveRedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F43F5E"));

            // Buttons & Controls
            res["PillButtonBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F161C"));
            res["PillButtonHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#152028"));
            res["PillButtonBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2E38"));
            res["HeroActionBtnTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#030D0C"));

            // Switches & Progress
            res["TrackBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0E141C"));
            res["TrackThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            res["ScrollThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A242E"));
            res["ScrollThumbHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F2B0"));

            // Badges
            res["AdminBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#081D17"));
            res["AdminBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F2B0"));
            res["AdminBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F2B0"));

            res["SafetyBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#081D17"));
            res["SafetyBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F2B0"));
            res["SafetyBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00F2B0"));

            res["WarningBadgeBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#261C0D"));
            res["WarningBadgeBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            res["WarningBadgeTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));

            // Modals
            res["ModalSurfaceBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B0F14"));
            res["ModalBackdropBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77030608"));

            // Hero Gradients
            var heroGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00F2B0"), 0.0));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0DD3BA"), 0.5));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#06B6D4"), 1.0));
            res["BrandHeroGradientBrush"] = heroGrad;

            var heroGradTop = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00F2B0"), 0.0));
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0DD3BA"), 0.5));
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#06B6D4"), 1.0));
            res["BrandHeroGradientBrushTop"] = heroGradTop;

            var heroBtn = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00F2B0"), 0.0));
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0DD3BA"), 0.5));
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00BFA5"), 1.0));
            res["HeroActionBtnBrush"] = heroBtn;

            var heroBtnPressed = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00D49B"), 0.0));
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0BB8A2"), 0.5));
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00A38D"), 1.0));
            res["HeroActionBtnPressedBrush"] = heroBtnPressed;

            var outerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#121C20"), 0.0));
            outerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#091014"), 1.0));
            res["DoubleBezelOuterBrush"] = outerGrad;

            var innerGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0D161A"), 0.0));
            innerGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#060A0D"), 1.0));
            res["DoubleBezelInnerBrush"] = innerGrad;

            var glassActive = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            glassActive.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#2000F2B0"), 0.0));
            glassActive.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#100DD3BA"), 0.6));
            glassActive.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0800BFA5"), 1.0));
            res["GlassSurfaceActiveBrush"] = glassActive;

            var glassTopBorder = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            glassTopBorder.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#3300F2B0"), 0.0));
            glassTopBorder.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0D0DD3BA"), 0.5));
            glassTopBorder.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00000000"), 1.0));
            res["GlassTopBorderBrush"] = glassTopBorder;
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
            res["SurfaceCardSelectedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FDF4"));
            res["SurfaceSubCardBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["HairlineBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
            res["AccentBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));

            // Deep, crisp Slate typography for effortless readability without eye strain
            res["TextHighBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            res["TextMediumBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));

            // High-contrast Emerald/Teal brand accents in light mode
            res["ElectricCyanBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D9488"));
            res["AccentBlueBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
            res["AccentIndigoBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
            res["SuccessGlowBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
            res["DestructiveRedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));

            // Buttons & Controls (Clean Slate 50 with crisp borders)
            res["PillButtonBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["PillButtonHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            res["PillButtonBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
            res["HeroActionBtnTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));

            // Switches & Progress
            res["TrackBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
            res["TrackThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            res["ScrollThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
            res["ScrollThumbHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0D9488"));

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

            // Hero Gradients (Refined Teal -> Emerald)
            var heroGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0D9488"), 0.0));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#059669"), 0.5));
            heroGrad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#047857"), 1.0));
            res["BrandHeroGradientBrush"] = heroGrad;

            var heroGradTop = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0D9488"), 0.0));
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#059669"), 0.5));
            heroGradTop.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#047857"), 1.0));
            res["BrandHeroGradientBrushTop"] = heroGradTop;

            var heroBtn = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0D9488"), 0.0));
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#059669"), 0.5));
            heroBtn.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#047857"), 1.0));
            res["HeroActionBtnBrush"] = heroBtn;

            var heroBtnPressed = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0F766E"), 0.0));
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#047857"), 0.5));
            heroBtnPressed.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#065F46"), 1.0));
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
            glassActiveLight.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1E0D9488"), 0.0));
            glassActiveLight.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0F059669"), 1.0));
            res["GlassSurfaceActiveBrush"] = glassActiveLight;

            var glassTopBorderLight = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            glassTopBorderLight.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#330D9488"), 0.0));
            glassTopBorderLight.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#00000000"), 1.0));
            res["GlassTopBorderBrush"] = glassTopBorderLight;
        }
    }
}
