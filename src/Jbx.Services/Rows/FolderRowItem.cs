namespace Jbx.Services.Rows;

public sealed record FolderRowItem(FolderEntry Entry, int PositionInApplication, int FoldersInApplication) : RowItem(Level: 2)
{
    public bool IsExpanded { get; init; }
}
