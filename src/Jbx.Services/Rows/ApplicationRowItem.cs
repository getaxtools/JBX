namespace Jbx.Services.Rows;

public sealed record ApplicationRowItem(ApplicationEntry Entry, int VisibleFolderCount, bool IsExpanded) : RowItem(Level: 1);
