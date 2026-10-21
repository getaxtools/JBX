using Jbx.Platform.Windows.FileSystem;
using Xunit;

namespace Jbx.Platform.Windows.Tests.FileSystem;

public sealed class WindowsPathCanonicalizerTests
{
    private readonly WindowsPathCanonicalizer _canonicalizer = new(new MappedDrives(new Dictionary<char, string> { ['Z'] = @"\\fs01.contoso.test\AppLogs" }));

    [Fact]
    public void Treats_a_mapped_drive_and_its_unc_path_as_the_same_folder()
    {
        var throughDrive = _canonicalizer.TryCanonicalize(@"Z:\prod\Billing");
        var throughUnc = _canonicalizer.TryCanonicalize(@"\\fs01.contoso.test\AppLogs\prod\Billing");

        Assert.Equal(throughUnc, throughDrive);
        Assert.Equal(@"Z:\prod\Billing", throughDrive?.Display);
    }

    [Theory]
    [InlineData(@"C:\Logs\Api\", @"C:\logs\api")]
    [InlineData(@"C:/Logs/Api", @"c:\logs\API")]
    [InlineData(@"\\?\C:\Logs\Api", @"C:\Logs\Api")]
    [InlineData(@"\\?\UNC\fs01.contoso.test\AppLogs\prod", @"\\FS01.CONTOSO.TEST\applogs\prod\")]
    [InlineData(@"C:\Logs\Old\..\Api", @"C:\Logs\Api")]
    public void Compares_equal_regardless_of_case_separators_prefixes_and_dot_segments(string first, string second)
    {
        Assert.Equal(_canonicalizer.TryCanonicalize(second), _canonicalizer.TryCanonicalize(first));
    }

    [Theory]
    [InlineData(@"C:\", @"C:\")]
    [InlineData(@"C:\Logs\", @"C:\Logs")]
    [InlineData(@"\\fs01.contoso.test\AppLogs\", @"\\fs01.contoso.test\AppLogs")]
    public void Keeps_roots_whole_while_trimming_trailing_separators(string input, string display)
    {
        Assert.Equal(display, _canonicalizer.TryCanonicalize(input)?.Display);
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"logs\api")]
    [InlineData(@"C:logs")]
    [InlineData(@"\\.\pipe\jbx")]
    public void Rejects_text_that_is_not_an_absolute_path(string input)
    {
        Assert.Null(_canonicalizer.TryCanonicalize(input));
    }

    [Fact]
    public void Finds_the_parent_folder_and_the_file_name()
    {
        var file = _canonicalizer.TryCanonicalize(@"C:\Logs\Api\api-2026-10-20.log")!;

        Assert.Equal(@"C:\Logs\Api", _canonicalizer.Parent(file)?.Display);
        Assert.Equal("api-2026-10-20.log", _canonicalizer.FileName(file));
        Assert.Null(_canonicalizer.Parent(_canonicalizer.TryCanonicalize(@"C:\")!));
    }

    [Fact]
    public void Reports_a_local_drive_as_not_mapped()
    {
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)![0];

        Assert.Null(new NetworkDriveResolver().TryResolve(systemDrive));
    }

    private sealed class MappedDrives(Dictionary<char, string> drives) : IMappedDriveResolver
    {
        public string? TryResolve(char driveLetter)
        {
            return drives.TryGetValue(char.ToUpperInvariant(driveLetter), out var remote) ? remote : null;
        }
    }
}
