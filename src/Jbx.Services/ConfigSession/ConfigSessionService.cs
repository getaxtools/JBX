using Jbx.Core.Config;
using Jbx.Core.Diagnostics;
using Jbx.Platform;
using Jbx.Services.Probing;
using Jbx.Services.Settings;

namespace Jbx.Services.ConfigSession;

/// <summary>
/// The config this window is showing. Opening or reloading parses into a new document and swaps it in
/// only when it is valid, so a bad save by a config owner never leaves the user with nothing.
/// </summary>
public sealed class ConfigSessionService(
    IFileSystem fileSystem,
    FileSystemGate gate,
    IPathCanonicalizer paths,
    ConfigLoader loader,
    SettingsService settings,
    IDiagnosticLog diagnosticLog) : IDisposable
{
    public static readonly TimeSpan ConfigReadTimeout = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim _openGate = new(1, 1);
    private FileStat? _lastSeen;

    public event EventHandler? DocumentChanged;

    public ConfigDocument? Document { get; private set; }

    public CanonicalPath? Path { get; private set; }

    public IReadOnlyList<ConfigProblem> Warnings { get; private set; } = [];

    public IReadOnlyList<string> RecentConfigPaths => settings.Current.RecentConfigPaths;

    public Task<ConfigOpenOutcome> OpenRememberedAsync(CancellationToken cancellationToken)
    {
        var remembered = settings.Current.LastConfigPath;
        return remembered is null
            ? Task.FromResult(new ConfigOpenOutcome(ConfigOpenStatus.NothingRemembered, null, null))
            : OpenAsync(remembered, cancellationToken);
    }

    public Task<ConfigOpenOutcome> ReloadAsync(CancellationToken cancellationToken)
    {
        return Path is null
            ? Task.FromResult(new ConfigOpenOutcome(ConfigOpenStatus.NothingRemembered, null, null))
            : OpenAsync(Path.Display, cancellationToken);
    }

    public async Task<ConfigOpenOutcome> OpenAsync(string pathText, CancellationToken cancellationToken)
    {
        var path = paths.TryCanonicalize(pathText);
        if (path is null)
        {
            return new ConfigOpenOutcome(ConfigOpenStatus.InvalidPath, pathText, null);
        }

        await _openGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Stat started with the read, not after it: a save landing during the read then still counts as a
            // change. Started together, a dead share costs one timeout rather than two.
            var statOfRead = StatAsync(path, cancellationToken);
            var outcome = await ReadAndLoadAsync(path, cancellationToken).ConfigureAwait(false);
            var seenBeforeRead = await statOfRead.ConfigureAwait(false);
            if (outcome is { Succeeded: true, Load: { } load })
            {
                _lastSeen = seenBeforeRead;
                await AcceptAsync(path, load, cancellationToken).ConfigureAwait(false);
            }

            return outcome;
        }
        finally
        {
            _openGate.Release();
        }
    }

    /// <summary>
    /// True once for each change to the open config's size or write time since it was opened or last
    /// checked, so a broken save is reported once rather than on every check.
    /// </summary>
    public async Task<bool> HasChangedOnDiskAsync(CancellationToken cancellationToken)
    {
        if (Path is not { } path)
        {
            return false;
        }

        var stat = await StatAsync(path, cancellationToken).ConfigureAwait(false);
        if (stat is null || _lastSeen is null || (stat.Length == _lastSeen.Length && stat.LastWriteUtc == _lastSeen.LastWriteUtc))
        {
            _lastSeen ??= stat;
            return false;
        }

        _lastSeen = stat;
        diagnosticLog.Write(new AppEvent(EventCode.ConfigChangedOnDisk, EventSeverity.Information), new ResourceDetail(new ResourceItem(ResourceKind.ConfigFile, path.Display)));
        return true;
    }

    public void Dispose()
    {
        _openGate.Dispose();
    }

    private async Task<FileStat?> StatAsync(CanonicalPath path, CancellationToken cancellationToken)
    {
        var outcome = await gate.RunAsync(() => fileSystem.StatFileAsync(path), ConfigReadTimeout, cancellationToken).ConfigureAwait(false);
        return outcome is { IsTimedOut: false, Value.Reachability: Reachability.Reachable } ? outcome.Value : null;
    }

    private async Task<ConfigOpenOutcome> ReadAndLoadAsync(CanonicalPath path, CancellationToken cancellationToken)
    {
        var detail = new ResourceDetail(new ResourceItem(ResourceKind.ConfigFile, path.Display));
        var read = await gate.RunAsync(() => fileSystem.ReadFileAsync(path, ConfigParser.MaxConfigSizeBytes), ConfigReadTimeout, cancellationToken).ConfigureAwait(false);
        var status = ReadStatus(read);
        if (status != ConfigOpenStatus.Opened || read.Value is not { } content)
        {
            diagnosticLog.Write(new AppEvent(status == ConfigOpenStatus.TooLarge ? EventCode.ConfigTooLarge : EventCode.ConfigReadFailed, EventSeverity.Warning, new EventMeasure(MeasureKind.HResult, read.Value?.ErrorCode ?? 0)), detail);
            return new ConfigOpenOutcome(status, path.Display, null);
        }

        var load = loader.Load(new ConfigSource(paths.FileName(path), content.Bytes));
        if (load.HasErrors)
        {
            diagnosticLog.Write(new AppEvent(EventCode.ConfigValidationFailed, EventSeverity.Warning, new EventMeasure(MeasureKind.Count, load.Errors.Count)), detail);
            return new ConfigOpenOutcome(ConfigOpenStatus.Invalid, path.Display, load);
        }

        diagnosticLog.Write(new AppEvent(EventCode.ConfigLoaded, EventSeverity.Information, new EventMeasure(MeasureKind.Count, load.Document?.Applications.Length ?? 0)), detail);
        return new ConfigOpenOutcome(ConfigOpenStatus.Opened, path.Display, load);
    }

    private static ConfigOpenStatus ReadStatus(GateResult<FileContent> read)
    {
        if (read.IsTimedOut || read.Value is null)
        {
            return ConfigOpenStatus.Timeout;
        }

        if (read.Value.IsTooLarge)
        {
            return ConfigOpenStatus.TooLarge;
        }

        return read.Value.Reachability switch
        {
            Reachability.Reachable => ConfigOpenStatus.Opened,
            Reachability.NotFound => ConfigOpenStatus.NotFound,
            Reachability.Denied => ConfigOpenStatus.Denied,
            Reachability.Timeout => ConfigOpenStatus.Timeout,
            _ => ConfigOpenStatus.Unreadable,
        };
    }

    private async Task AcceptAsync(CanonicalPath path, ConfigLoadResult load, CancellationToken cancellationToken)
    {
        Document = load.Document;
        Path = path;
        Warnings = load.Warnings;
        await settings.UpdateAsync(current => current with
        {
            LastConfigPath = path.Display,
            RecentConfigPaths = [.. RecentWith(path, current.RecentConfigPaths)],
        }, cancellationToken).ConfigureAwait(false);
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<string> RecentWith(CanonicalPath opened, IReadOnlyList<string> recents)
    {
        yield return opened.Display;
        foreach (var recent in recents)
        {
            var canonical = paths.TryCanonicalize(recent);
            if (canonical is not null && canonical != opened)
            {
                yield return recent;
            }
        }
    }
}
