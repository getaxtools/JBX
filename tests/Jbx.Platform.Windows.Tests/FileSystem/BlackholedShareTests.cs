using System.Diagnostics;
using Jbx.Platform.Windows.FileSystem;
using Jbx.Services.Probing;
using Jbx.Testing;
using Xunit;

namespace Jbx.Platform.Windows.Tests.FileSystem;

/// <summary>
/// Real calls to TEST-NET-1 addresses, which are never routed: the SMB connect blocks until the
/// network gives up, which is the behaviour the probe is designed around. A mock cannot reproduce it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class BlackholedShareTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("jbx-blackhole-tests-");
    private readonly WindowsPathCanonicalizer _paths = new(new NetworkDriveResolver());
    private readonly FileSystemGate _gate = new(TimeProvider.System);
    private readonly ReachabilityProbe _probe;

    public BlackholedShareTests()
    {
        _probe = new ReachabilityProbe(new WindowsFileSystem(_paths), _gate, new ProbeCache(TimeProvider.System), new RecordingDiagnosticLog());
    }

    public void Dispose()
    {
        _root.Delete(recursive: true);
    }

    [Fact]
    public async Task Gives_up_on_an_unreachable_share_within_the_probe_timeout()
    {
        var stopwatch = Stopwatch.StartNew();

        var result = await _probe.ProbeFolderAsync(_paths.TryCanonicalize(@"\\192.0.2.1\jbx-blackhole")!, TestContext.Current.CancellationToken);

        Assert.True(stopwatch.Elapsed < ReachabilityProbe.ProbeTimeout + TimeSpan.FromMilliseconds(500), $"took {stopwatch.Elapsed}");
        Assert.NotEqual(Reachability.Reachable, result.Reachability);
        TestContext.Current.TestOutputHelper?.WriteLine(result.Reachability == Reachability.Timeout
            ? "The connect blocked, so the timeout path was exercised."
            : $"The network refused the connect at once ({result.Reachability}); the hang path was not exercised here.");
    }

    [Fact]
    public async Task Resolves_thirty_rows_with_four_dead_shares_within_six_seconds()
    {
        var reachable = Enumerable.Range(0, 26).Select(index => _root.CreateSubdirectory($"app-{index}").FullName);
        var dead = Enumerable.Range(1, 4).Select(host => $@"\\192.0.2.{host}\jbx-blackhole");
        var stopwatch = Stopwatch.StartNew();

        var results = await Task.WhenAll(reachable.Concat(dead)
            .Select(path => _probe.ProbeFolderAsync(_paths.TryCanonicalize(path)!, TestContext.Current.CancellationToken)));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(6), $"took {stopwatch.Elapsed}");
        Assert.Equal(26, results.Count(result => result.Reachability == Reachability.Reachable));
    }
}
