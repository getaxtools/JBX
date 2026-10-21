using Jbx.Core.Config;

namespace Jbx.Services.ConfigSession;

/// <summary>What happened when a config was opened. <see cref="Load"/> carries every error when the file was read but invalid.</summary>
public sealed record ConfigOpenOutcome(ConfigOpenStatus Status, string? PathText, ConfigLoadResult? Load)
{
    public bool Succeeded => Status == ConfigOpenStatus.Opened;
}
