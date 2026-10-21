using Jbx.Platform;

namespace Jbx.Services.Rows;

public sealed record FileRowItem(FolderEntry Folder, FileEntry File, int PositionInFolder, int FilesInFolder) : RowItem(Level: 3);
