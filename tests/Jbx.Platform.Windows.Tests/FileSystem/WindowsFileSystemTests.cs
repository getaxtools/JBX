using System.Security.AccessControl;
using System.Security.Principal;
using Jbx.Platform.Windows.FileSystem;
using Xunit;

namespace Jbx.Platform.Windows.Tests.FileSystem;

public sealed class WindowsFileSystemTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("jbx-fs-tests-");
    private readonly WindowsPathCanonicalizer _paths = new(new NetworkDriveResolver());
    private readonly WindowsFileSystem _fileSystem;
    private DirectoryInfo? _deniedFolder;
    private FileSystemAccessRule? _denyRule;

    public WindowsFileSystemTests()
    {
        _fileSystem = new WindowsFileSystem(_paths);
    }

    public void Dispose()
    {
        if (_deniedFolder is not null && _denyRule is not null)
        {
            var security = _deniedFolder.GetAccessControl();
            security.RemoveAccessRule(_denyRule);
            _deniedFolder.SetAccessControl(security);
        }

        _root.Delete(recursive: true);
    }

    [Fact]
    public async Task Reports_an_existing_empty_folder_as_reachable()
    {
        var result = await _fileSystem.ProbeFolderAsync(Path(_root.FullName));

        Assert.Equal(Reachability.Reachable, result.Reachability);
    }

    [Fact]
    public async Task Reports_a_missing_folder_as_not_found()
    {
        var result = await _fileSystem.ProbeFolderAsync(Path(System.IO.Path.Combine(_root.FullName, "missing")));

        Assert.Equal(Reachability.NotFound, result.Reachability);
    }

    [Fact]
    public async Task Reports_a_folder_the_user_cannot_list_as_denied_not_missing()
    {
        _deniedFolder = _root.CreateSubdirectory("denied");
        _denyRule = new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User!,
            FileSystemRights.ListDirectory | FileSystemRights.ReadData,
            AccessControlType.Deny);
        var security = _deniedFolder.GetAccessControl();
        security.AddAccessRule(_denyRule);
        _deniedFolder.SetAccessControl(security);

        var probe = await _fileSystem.ProbeFolderAsync(Path(_deniedFolder.FullName));
        var listing = await _fileSystem.ListFilesAsync(Path(_deniedFolder.FullName), new ListingRequest(null, 10));

        Assert.Equal(Reachability.Denied, probe.Reachability);
        Assert.Equal(Reachability.Denied, listing.Reachability);
    }

    [Fact]
    public async Task Probes_a_file_and_reports_a_missing_one()
    {
        var file = System.IO.Path.Combine(_root.FullName, "api.log");
        File.WriteAllText(file, "line");

        var present = await _fileSystem.ProbeFileAsync(Path(file));
        var missing = await _fileSystem.ProbeFileAsync(Path(file + ".old"));

        Assert.Equal(Reachability.Reachable, present.Reachability);
        Assert.Equal(Reachability.NotFound, missing.Reachability);
    }

    [Fact]
    public async Task Lists_the_newest_matching_files_first_and_reports_truncation()
    {
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var day = 0; day < 30; day++)
        {
            var file = System.IO.Path.Combine(_root.FullName, $"api-{day:D2}.log");
            File.WriteAllText(file, "line");
            File.SetLastWriteTimeUtc(file, start.AddDays(day));
        }

        File.WriteAllText(System.IO.Path.Combine(_root.FullName, "readme.txt"), "not a log");

        var listing = await _fileSystem.ListFilesAsync(Path(_root.FullName), new ListingRequest("*.log", 10));

        Assert.Equal(Reachability.Reachable, listing.Reachability);
        Assert.True(listing.IsTruncated);
        Assert.Equal(30, listing.MatchingFileCount);
        Assert.Equal(Enumerable.Range(20, 10).Reverse().Select(day => $"api-{day:D2}.log"), listing.NewestFirst.Select(entry => entry.Name));
    }

    [Fact]
    public async Task Reads_part_of_a_log_while_its_writer_still_holds_it_open()
    {
        var file = System.IO.Path.Combine(_root.FullName, "api.log");
        using var writer = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);
        writer.Write("0123456789"u8);
        writer.Flush();

        var middle = await _fileSystem.ReadRangeAsync(Path(file), 3, 4);
        var pastTheEnd = await _fileSystem.ReadRangeAsync(Path(file), 20, 4);

        Assert.Equal(Reachability.Reachable, middle.Reachability);
        Assert.Equal("3456"u8.ToArray(), middle.Bytes.ToArray());
        Assert.Equal(10, middle.FileLength);
        Assert.True(pastTheEnd.Bytes.IsEmpty);
    }

    [Fact]
    public async Task States_a_file_and_reports_a_missing_one_as_not_found()
    {
        var file = System.IO.Path.Combine(_root.FullName, "api.log");
        File.WriteAllText(file, "twelve bytes");
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc));

        var present = await _fileSystem.StatFileAsync(Path(file));
        var missing = await _fileSystem.StatFileAsync(Path(file + ".old"));

        Assert.Equal((Reachability.Reachable, 12L), (present.Reachability, present.Length));
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero), present.LastWriteUtc);
        Assert.Equal(Reachability.NotFound, missing.Reachability);
    }

    private CanonicalPath Path(string path)
    {
        return _paths.TryCanonicalize(path)!;
    }
}
