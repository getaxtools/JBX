using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Jbx.Platform;

namespace Jbx.Desktop.Infrastructure;

/// <summary>
/// Follows a Windows contrast theme. Fluent has no contrast variant and takes palettes only for Light
/// and Dark, so the system colours override whichever of the two matches the contrast background, for
/// both Fluent's palette and the app's own tokens. Colour never carries meaning on its own: every
/// status also has an icon and a word.
/// </summary>
internal static class ContrastThemes
{
    public static void Follow(Application application, ISystemContrast contrast)
    {
        Apply(application, contrast.Current);
        if (application.PlatformSettings is { } settings)
        {
            settings.ColorValuesChanged += (_, _) => Apply(application, contrast.Current);
        }
    }

    public static void Apply(Application application, ContrastColors? colors)
    {
        var fluent = application.Styles.OfType<FluentTheme>().FirstOrDefault();
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            application.Resources.ThemeDictionaries.Remove(variant);
            fluent?.Palettes.Remove(variant);
        }

        if (colors is null)
        {
            application.RequestedThemeVariant = ThemeVariant.Default;
            return;
        }

        var contrastVariant = colors.IsLight ? ThemeVariant.Light : ThemeVariant.Dark;
        application.Resources.ThemeDictionaries[contrastVariant] = Tokens(colors);
        if (fluent is not null)
        {
            fluent.Palettes[contrastVariant] = Palette(colors);
        }

        application.RequestedThemeVariant = contrastVariant;
    }

    private static ResourceDictionary Tokens(ContrastColors colors)
    {
        var window = Brush(colors.Window);
        var text = Brush(colors.WindowText);
        var highlight = Brush(colors.Highlight);
        var hotLight = Brush(colors.HotLight);
        return new ResourceDictionary
        {
            ["SurfaceBaseBrush"] = window,
            ["SurfaceContentBrush"] = window,
            ["SurfaceCardBrush"] = window,
            ["SurfaceHoverBrush"] = highlight,
            ["SurfaceSelectedBrush"] = highlight,
            ["StrokeDividerBrush"] = text,
            ["StrokeControlBrush"] = text,
            ["TextPrimaryBrush"] = text,
            ["TextSecondaryBrush"] = text,
            ["StatusReachableBrush"] = text,
            ["StatusProblemBrush"] = text,
            ["StatusWarningBrush"] = text,
            ["StatusNeutralBrush"] = text,
            ["ProductionBrush"] = hotLight,
            ["ProductionBackgroundBrush"] = window,
            ["FavouriteBrush"] = hotLight,
            ["AccentTextBrush"] = hotLight,
            ["MatchHighlightBrush"] = highlight,
            ["ScrimBrush"] = Brushes.Transparent,
            ["AccentButtonForeground"] = Brush(colors.HighlightText),
            ["AccentButtonForegroundPointerOver"] = Brush(colors.HighlightText),
            ["AccentButtonForegroundPressed"] = Brush(colors.HighlightText),
            ["ButtonBorderBrush"] = Brush(colors.ButtonText),
            ["ButtonBorderBrushPointerOver"] = highlight,
            ["ButtonBackgroundPointerOver"] = highlight,
            ["ButtonForegroundPointerOver"] = Brush(colors.HighlightText),
            ["ButtonForegroundDisabled"] = Brush(colors.GrayText),
            ["ButtonBorderBrushDisabled"] = Brush(colors.GrayText),
        };
    }

    private static ColorPaletteResources Palette(ContrastColors colors)
    {
        var window = Color.FromUInt32(colors.Window);
        var text = Color.FromUInt32(colors.WindowText);
        var highlight = Color.FromUInt32(colors.Highlight);
        var disabled = Color.FromUInt32(colors.GrayText);
        var face = Color.FromUInt32(colors.ButtonFace);
        return new ColorPaletteResources
        {
            Accent = highlight,
            RegionColor = window,
            AltHigh = window,
            AltLow = window,
            AltMedium = window,
            AltMediumHigh = window,
            AltMediumLow = window,
            BaseHigh = text,
            BaseLow = face,
            BaseMedium = text,
            BaseMediumHigh = text,
            BaseMediumLow = face,
            ChromeAltLow = text,
            ChromeBlackHigh = text,
            ChromeBlackLow = text,
            ChromeBlackMedium = text,
            ChromeBlackMediumLow = text,
            ChromeDisabledHigh = disabled,
            ChromeDisabledLow = disabled,
            ChromeGray = text,
            ChromeHigh = text,
            ChromeLow = face,
            ChromeMedium = face,
            ChromeMediumLow = face,
            ChromeWhite = window,
            ErrorText = text,
            ListLow = highlight,
            ListMedium = highlight,
        };
    }

    private static SolidColorBrush Brush(uint argb)
    {
        return new SolidColorBrush(Color.FromUInt32(argb));
    }
}
