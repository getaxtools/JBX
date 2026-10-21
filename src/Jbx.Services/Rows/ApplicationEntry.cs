using Jbx.Core.Config;

namespace Jbx.Services.Rows;

public sealed record ApplicationEntry(ApplicationDefinition Application, IReadOnlyList<FolderEntry> Folders);
