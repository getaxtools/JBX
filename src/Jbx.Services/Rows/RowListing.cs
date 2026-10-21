namespace Jbx.Services.Rows;

/// <summary>The rows to show plus the counts the status line needs ("3 of 340 folders").</summary>
public sealed record RowListing(IReadOnlyList<RowItem> Rows, int MatchingFolderCount, int TotalFolderCount);
