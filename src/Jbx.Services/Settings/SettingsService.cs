using System.Text.Json;
using Jbx.Core.Diagnostics;
using Jbx.Platform;

namespace Jbx.Services.Settings;

public sealed class SettingsService(ISettingsFileStore store, IDiagnosticLog diagnosticLog) : IDisposable
{
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public event EventHandler? Changed;

    public UserSettings Current { get; private set; } = UserSettings.Defaults;

    public async Task<SettingsLoadOutcome> LoadAsync(CancellationToken cancellationToken)
    {
        var read = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        switch (read.Status)
        {
            case SettingsFileStatus.Missing:
                Current = UserSettings.Defaults;
                return new SettingsLoadOutcome(SettingsLoadStatus.FirstRun, null);
            case SettingsFileStatus.Unreadable:
                Current = UserSettings.Defaults;
                diagnosticLog.Write(new AppEvent(EventCode.SettingsSetAside, EventSeverity.Warning), Location());
                return new SettingsLoadOutcome(SettingsLoadStatus.Unreadable, null);
            default:
                return await AcceptOrSetAsideAsync(read.Content ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> UpdateAsync(Func<UserSettings, UserSettings> change, CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Current = Normalise(change(Current));
            Changed?.Invoke(this, EventArgs.Empty);
            var content = JsonSerializer.Serialize(Current, SettingsJsonContext.Default.UserSettings);
            var written = await store.TryWriteAsync(content, cancellationToken).ConfigureAwait(false);
            if (!written)
            {
                diagnosticLog.Write(new AppEvent(EventCode.SettingsWriteFailed, EventSeverity.Warning), Location());
            }

            return written;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public void Dispose()
    {
        _writeGate.Dispose();
    }

    private async Task<SettingsLoadOutcome> AcceptOrSetAsideAsync(string content, CancellationToken cancellationToken)
    {
        var settings = TryDeserialise(content);
        if (settings is not null && settings.SchemaVersion == UserSettings.CurrentSchemaVersion)
        {
            Current = Normalise(settings);
            diagnosticLog.Write(new AppEvent(EventCode.SettingsLoaded, EventSeverity.Information, new EventMeasure(MeasureKind.Count, Current.Favourites.Length)));
            return new SettingsLoadOutcome(SettingsLoadStatus.Loaded, null);
        }

        Current = UserSettings.Defaults;
        var setAsideName = await store.TrySetAsideAsync(cancellationToken).ConfigureAwait(false);
        diagnosticLog.Write(new AppEvent(EventCode.SettingsSetAside, EventSeverity.Warning), Location());
        return new SettingsLoadOutcome(SettingsLoadStatus.SetAside, setAsideName);
    }

    private static UserSettings? TryDeserialise(string content)
    {
        try
        {
            return JsonSerializer.Deserialize(content, SettingsJsonContext.Default.UserSettings);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static UserSettings Normalise(UserSettings settings)
    {
        var recents = settings.RecentConfigPaths.IsDefault ? [] : settings.RecentConfigPaths;
        var favourites = settings.Favourites.IsDefault ? [] : settings.Favourites;
        return settings with
        {
            RecentConfigPaths = [.. recents.Where(path => !string.IsNullOrWhiteSpace(path)).Take(UserSettings.MaxRecentConfigs)],
            Favourites = [.. favourites.Where(favourite => !string.IsNullOrWhiteSpace(favourite.Path))],
            Window = settings.Window ?? new WindowPlacement(),
        };
    }

    private ResourceDetail Location()
    {
        return new ResourceDetail(new ResourceItem(ResourceKind.Path, store.Location));
    }
}
