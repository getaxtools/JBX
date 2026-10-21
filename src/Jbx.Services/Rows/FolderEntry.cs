using Jbx.Core.Config;
using Jbx.Platform;

namespace Jbx.Services.Rows;

/// <summary>One folder variation resolved for one environment. <see cref="Path"/> is null when its template did not resolve.</summary>
public sealed record FolderEntry(ApplicationDefinition Application, FolderVariation Folder, CanonicalPath? Path)
{
    public string Key => $"{Application.Id}/{Folder.Label}";
}
