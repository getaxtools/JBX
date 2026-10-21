using Jbx.Desktop.Resources;
using Jbx.Services.Rows;

namespace Jbx.Desktop.Text;

internal static class TreeText
{
    public static string Note(FolderNoteRowItem note)
    {
        return note.Note switch
        {
            FolderNote.Loading => Strings.Tree_LoadingFiles,
            FolderNote.Empty => note.Folder.Folder.LogFilePattern is { } pattern
                ? Strings.FormatDetails_NoFilesMatch(pattern)
                : Strings.Details_FolderEmpty,
            FolderNote.Failed => note.Folder.Path is null
                ? Strings.Rows_PathUnresolved
                : Strings.FormatTree_FilesFailed(StatusText.Label(note.Reachability)),
            _ => Strings.FormatTree_MoreFiles(note.ShownFileCount, note.MatchingFileCount),
        };
    }
}
