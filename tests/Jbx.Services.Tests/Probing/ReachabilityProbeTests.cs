using Jbx.Core.Diagnostics;
using Jbx.Platform;
using Jbx.Services.Probing;
using Jbx.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Jbx.Services.Tests.Probing;

public sealed class ReachabilityProbeTests : IDisposable
{
    private const string Share = @"\\fs01.contoso.test\AppLogs";

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDiagnosticLog _log = new();
    private readonly SimplePathCanonicalizer _paths = new(new Dictionary<char, string> { ['Z'] = Share });
    private readonly InMemoryFileSystem _fileSystem;
    private readonly FileSystemGate _gate;
    private readonly ReachabilityProbe _probe;

    public ReachabilityProbeTests()
    {
        _fileSystem = new InMemoryFileSystem(_paths);
        _gate = new FileSystemGate(_time);
        _probe = new ReachabilityProbe(_fileSystem, _gate, new ProbeCache(_time), _log);
    }

    public void Dispose()
    {
        _fileSystem.ReleaseBlackholed();
        _gate.Dispose();
    }

    [Theory]
    [InlineData(FolderBehaviour.Present, Reachability.Reachable)]
    [InlineData(FolderBehaviour.Missing, Reachability.NotFound)]
    [InlineData(FolderBehaviour.Denied, Reachability.Denied)]
    [InlineData(FolderBehaviour.Failing, Reachability.Unknown)]
    public async Task Reports_each_outcome_distinctly(FolderBehaviour behaviour, Reachability expected)
    {
        _fileSystem.AddFolder(Share + @"\prod\Billing", behaviour);

        var result = await _probe.ProbeFolderAsync(_paths.From(Share + @"\prod\Billing"), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Reachability);
    }

    [Fact]
    public async Task Times_out_a_blackholed_share_when_the_probe_timeout_passes()
    {
        _fileSystem.AddFolder(Share + @"\prod", FolderBehaviour.Blackholed);

        var pending = _probe.ProbeFolderAsync(_paths.From(Share + @"\prod"), TestContext.Current.CancellationToken);
        _time.Advance(ReachabilityProbe.ProbeTimeout - TimeSpan.FromMilliseconds(1));
        var stillWaiting = !pending.IsCompleted;
        _time.Advance(TimeSpan.FromMilliseconds(1));
        var result = await pending;

        Assert.True(stillWaiting);
        Assert.Equal(Reachability.Timeout, result.Reachability);
        Assert.True(_log.Contains(EventCode.ProbeTimedOut));
    }

    [Fact]
    public async Task Keeps_the_windows_error_of_an_unknown_failure_in_the_log()
    {
        _fileSystem.AddFolder(Share + @"\prod", FolderBehaviour.Failing);

        await _probe.ProbeFolderAsync(_paths.From(Share + @"\prod"), TestContext.Current.CancellationToken);

        var logged = Assert.Single(_log.Events, appEvent => appEvent.Code == EventCode.ProbeFailed);
        Assert.Equal(new EventMeasure(MeasureKind.HResult, 1231), Assert.Single(logged.Measures));
        Assert.Equal(ResourceKind.Path, Assert.Single(_log.Details.OfType<ResourceDetail>().Single().Items).Kind);
    }

    [Fact]
    public async Task Never_runs_more_than_sixteen_calls_at_once()
    {
        var folders = Enumerable.Range(0, 100).Select(index => $@"{Share}\app-{index}").ToList();
        folders.ForEach(folder => _fileSystem.AddFolder(folder, FolderBehaviour.Blackholed));

        var probes = folders.Select(folder => _probe.ProbeFolderAsync(_paths.From(folder), TestContext.Current.CancellationToken)).ToList();
        await Eventually.TrueAsync(() => _fileSystem.InFlight == FileSystemGate.MaxConcurrentCalls);
        _time.Advance(ReachabilityProbe.ProbeTimeout);
        var results = await Task.WhenAll(probes);

        Assert.Equal(FileSystemGate.MaxConcurrentCalls, _fileSystem.MaxInFlight);
        Assert.All(results, result => Assert.Equal(Reachability.Timeout, result.Reachability));
    }

    [Fact]
    public async Task Keeps_a_slot_busy_until_an_abandoned_call_really_finishes()
    {
        var stuck = Enumerable.Range(0, FileSystemGate.MaxConcurrentCalls).Select(index => $@"{Share}\stuck-{index}").ToList();
        stuck.ForEach(folder => _fileSystem.AddFolder(folder, FolderBehaviour.Blackholed));
        _fileSystem.AddFolder(@"D:\Logs\first", FolderBehaviour.Present);
        _fileSystem.AddFolder(@"D:\Logs\second", FolderBehaviour.Present);
        var abandoned = stuck.Select(folder => _probe.ProbeFolderAsync(_paths.From(folder), TestContext.Current.CancellationToken)).ToList();
        await Eventually.TrueAsync(() => _fileSystem.InFlight == FileSystemGate.MaxConcurrentCalls);
        _time.Advance(ReachabilityProbe.ProbeTimeout);
        await Task.WhenAll(abandoned);

        var queued = _probe.ProbeFolderAsync(_paths.From(@"D:\Logs\first"), TestContext.Current.CancellationToken);
        _time.Advance(ReachabilityProbe.ProbeTimeout);
        var whileStuck = await queued;
        _fileSystem.ReleaseBlackholed();
        await Eventually.TrueAsync(() => _fileSystem.InFlight == 0);
        var afterRelease = await _probe.ProbeFolderAsync(_paths.From(@"D:\Logs\second"), TestContext.Current.CancellationToken);

        Assert.Equal(Reachability.Timeout, whileStuck.Reachability);
        Assert.Equal(Reachability.Reachable, afterRelease.Reachability);
    }

    [Fact]
    public async Task Probes_a_mapped_drive_and_its_unc_path_once_because_they_are_the_same_folder()
    {
        _fileSystem.AddFolder(Share + @"\prod\Billing");

        var throughDrive = await _probe.ProbeFolderAsync(_paths.From(@"Z:\prod\Billing"), TestContext.Current.CancellationToken);
        var throughUnc = await _probe.ProbeFolderAsync(_paths.From(Share + @"\PROD\billing\"), TestContext.Current.CancellationToken);

        Assert.Equal(Reachability.Reachable, throughDrive.Reachability);
        Assert.Equal(Reachability.Reachable, throughUnc.Reachability);
        Assert.Equal(1, _fileSystem.ProbeCount);
    }

    [Fact]
    public async Task Probes_again_once_the_cached_result_is_a_minute_old_or_forgotten()
    {
        var folder = _paths.From(Share + @"\prod");
        _fileSystem.AddFolder(folder.Display);

        await _probe.ProbeFolderAsync(folder, TestContext.Current.CancellationToken);
        await _probe.ProbeFolderAsync(folder, TestContext.Current.CancellationToken);
        _time.Advance(ProbeCache.TimeToLive);
        await _probe.ProbeFolderAsync(folder, TestContext.Current.CancellationToken);
        _probe.Forget([folder]);
        await _probe.ProbeFolderAsync(folder, TestContext.Current.CancellationToken);

        Assert.Equal(3, _fileSystem.ProbeCount);
    }
}
