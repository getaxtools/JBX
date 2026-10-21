using Jbx.Core.Config;

namespace Jbx.Services.Settings;

/// <summary>A tab to reopen. <see cref="SelectedFile"/> is -1 when its folders were showing.</summary>
public sealed record RestoredTab(DeploymentEnvironment Environment, IReadOnlyList<RestoredFile> Files, int SelectedFile);
