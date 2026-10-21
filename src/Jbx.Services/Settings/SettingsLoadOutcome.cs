namespace Jbx.Services.Settings;

/// <summary>How loading went, so the interface can tell the user once when their settings were set aside.</summary>
public sealed record SettingsLoadOutcome(SettingsLoadStatus Status, string? SetAsideFileName);
