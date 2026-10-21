using Jbx.Desktop.Features.About;
using Xunit;

namespace Jbx.Desktop.Tests.Shell;

public sealed class AboutTests
{
    [Fact]
    public void Carries_the_third_party_licences_inside_the_program()
    {
        var about = new AboutViewModel();

        Assert.Contains("YamlDotNet 18.1.0", about.ThirdPartyNotices, StringComparison.Ordinal);
        Assert.Contains("Avalonia 12.1.4", about.ThirdPartyNotices, StringComparison.Ordinal);
        Assert.Contains("MIT license", about.ThirdPartyNotices, StringComparison.Ordinal);
        Assert.DoesNotContain("ResxSourceGenerator", about.ThirdPartyNotices, StringComparison.Ordinal);
    }
}
