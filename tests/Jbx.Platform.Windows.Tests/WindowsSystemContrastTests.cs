using Xunit;

namespace Jbx.Platform.Windows.Tests;

public sealed class WindowsSystemContrastTests
{
    [Fact]
    public void Reads_the_contrast_setting_without_failing_and_gives_opaque_colours_when_it_is_on()
    {
        var contrast = new WindowsSystemContrast();

        var colors = contrast.Current;

        if (colors is not null)
        {
            Assert.All([colors.Window, colors.WindowText, colors.Highlight, colors.HighlightText], color => Assert.Equal(0xFF000000u, color & 0xFF000000u));
        }
    }

    [Theory]
    [InlineData(0xFF000000u, false)]
    [InlineData(0xFFFFFAEFu, true)]
    [InlineData(0xFF202020u, false)]
    public void Tells_a_light_contrast_theme_from_a_dark_one(uint window, bool expected)
    {
        var colors = new ContrastColors(window, 0xFFFFFFFF, 0xFF1AEBFF, 0xFF000000);

        Assert.Equal(expected, colors.IsLight);
    }
}
