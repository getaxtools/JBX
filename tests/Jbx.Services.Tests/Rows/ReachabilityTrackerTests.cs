using Jbx.Platform;
using Jbx.Services.Probing;
using Jbx.Services.Rows;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Services.Tests.Rows;

public sealed class ReachabilityTrackerTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly InMemoryFileSystem _fileSystem = new();
    private readonly FileSystemGate _gate;
    private readonly ReachabilityTracker _tracker;
    private readonly EnvironmentRows _production;

    public ReachabilityTrackerTests()
    {
        _gate = new FileSystemGate(_time);
        var probe = new ReachabilityProbe(_fileSystem, _gate, new ProbeCache(_time), new RecordingDiagnosticLog());
        _tracker = new ReachabilityTracker(probe, _time);
        var document = TestConfigs.FullExample();
        _production = EnvironmentRows.Resolve(document, document.FindEnvironment("prod")!, TestConfigs.Resolver(), SimplePathCanonicalizer.Instance);
    }

    public void Dispose()
    {
        _fileSystem.ReleaseBlackholed();
        _gate.Dispose();
    }

    [Fact]
    public async Task One_unreachable_folder_never_holds_back_the_others()
    {
        var folders = _production.Folders;
        _fileSystem.AddFolder(folders[0].Path!.Display, FolderBehaviour.Blackholed);
        _fileSystem.AddFolder(folders[1].Path!.Display, FolderBehaviour.Denied);
        _fileSystem.AddFolder(folders[2].Path!.Display);

        var checking = _tracker.CheckAsync(folders, TestContext.Current.CancellationToken);
        await Eventually.TrueAsync(() => _tracker.StatusOf(folders[2]).Reachability == Reachability.Reachable);
        var whileFirstIsStuck = folders.Select(folder => _tracker.StatusOf(folder).Reachability).ToList();
        _time.Advance(ReachabilityProbe.ProbeTimeout);
        await checking;

        Assert.Equal([Reachability.Checking, Reachability.Denied, Reachability.Reachable], whileFirstIsStuck);
        Assert.Equal(Reachability.Timeout, _tracker.StatusOf(folders[0]).Reachability);
    }

    [Fact]
    public async Task Drops_a_late_answer_when_a_newer_check_has_already_reported()
    {
        var folder = _production.Folders[0];
        _fileSystem.AddFolder(folder.Path!.Display, FolderBehaviour.Blackholed);
        var stale = _tracker.CheckAsync([folder], TestContext.Current.CancellationToken);
        await Eventually.TrueAsync(() => _fileSystem.InFlight == 1);

        _fileSystem.AddFolder(folder.Path.Display, FolderBehaviour.Present);
        await _tracker.RefreshAsync([folder], TestContext.Current.CancellationToken);
        _time.Advance(ReachabilityProbe.ProbeTimeout);
        await stale;

        Assert.Equal(Reachability.Reachable, _tracker.StatusOf(folder).Reachability);
    }

    [Fact]
    public async Task Announces_each_change_so_the_row_can_update()
    {
        var folder = _production.Folders[0];
        _fileSystem.AddFolder(folder.Path!.Display);
        var announced = new List<Reachability>();
        _tracker.StatusChanged += (_, entry) => announced.Add(_tracker.StatusOf(entry).Reachability);

        await _tracker.CheckAsync([folder], TestContext.Current.CancellationToken);

        Assert.Equal([Reachability.Checking, Reachability.Reachable], announced);
    }
}
