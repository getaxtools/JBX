namespace Jbx.Platform;

/// <summary>The colours of a contrast theme, as 0xAARRGGBB, named after the Windows system colours they come from.</summary>
public sealed record ContrastColors(uint Window, uint WindowText, uint Highlight, uint HighlightText)
{
    public uint HotLight { get; init; }

    public uint GrayText { get; init; }

    public uint ButtonFace { get; init; }

    public uint ButtonText { get; init; }

    /// <summary>True for a light background, such as the "Desert" theme, so other parts can follow the light variant.</summary>
    public bool IsLight
    {
        get
        {
            // Relative luminance weights from ITU-R BT.709.
            const double RedWeight = 0.2126;
            const double GreenWeight = 0.7152;
            const double BlueWeight = 0.0722;
            const double ChannelMax = 255;
            const double MidGrey = 0.5;
            var red = ((Window >> 16) & 0xFF) / ChannelMax;
            var green = ((Window >> 8) & 0xFF) / ChannelMax;
            var blue = (Window & 0xFF) / ChannelMax;
            return (RedWeight * red) + (GreenWeight * green) + (BlueWeight * blue) > MidGrey;
        }
    }
}
