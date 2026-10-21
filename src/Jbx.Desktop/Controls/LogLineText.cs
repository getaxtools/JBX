using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Jbx.Services.Logs;

namespace Jbx.Desktop.Controls;

/// <summary>
/// One log line with its search matches marked. Drawn directly rather than as a TextBlock of runs,
/// because a run per match means a control per match, and a log view shows thousands of matches.
/// </summary>
internal sealed class LogLineText : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<LogLineText, string?>(nameof(Text));

    public static readonly StyledProperty<IReadOnlyList<MatchSpan>?> HighlightsProperty =
        AvaloniaProperty.Register<LogLineText, IReadOnlyList<MatchSpan>?>(nameof(Highlights));

    public static readonly StyledProperty<IBrush?> HighlightBrushProperty =
        AvaloniaProperty.Register<LogLineText, IBrush?>(nameof(HighlightBrush));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<LogLineText>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<LogLineText>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<LogLineText>();

    static LogLineText()
    {
        AffectsMeasure<LogLineText>(TextProperty, FontFamilyProperty, FontSizeProperty);
        AffectsRender<LogLineText>(HighlightsProperty, HighlightBrushProperty, ForegroundProperty);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IReadOnlyList<MatchSpan>? Highlights
    {
        get => GetValue(HighlightsProperty);
        set => SetValue(HighlightsProperty, value);
    }

    public IBrush? HighlightBrush
    {
        get => GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        using var layout = CreateLayout();
        if (HighlightBrush is { } brush && Highlights is { Count: > 0 } highlights)
        {
            foreach (var highlight in highlights)
            {
                foreach (var rectangle in layout.HitTestTextRange(highlight.Start, highlight.Length))
                {
                    context.FillRectangle(brush, rectangle);
                }
            }
        }

        layout.Draw(context, default);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        using var layout = CreateLayout();
        return new Size(layout.WidthIncludingTrailingWhitespace, layout.Height);
    }

    private TextLayout CreateLayout()
    {
        return new TextLayout(Text ?? string.Empty, new Typeface(FontFamily), FontSize, Foreground);
    }
}
