using Jbx.Platform;
using Jbx.Services.Search;

namespace Jbx.Services.Rows;

/// <summary>
/// Turns resolved applications into the tree a tab shows: filtered, searched, sorted and flattened.
/// Works on memory only; it never probes or lists anything. Files appear only for folders whose
/// listing the caller already has.
/// </summary>
public static class RowFlattener
{
    public const int MaxFilesPerFolder = 100;

    public static RowListing Flatten(
        EnvironmentRows environment,
        RowQuery query,
        RowExpansion expansion,
        Func<FolderEntry, FolderStatus> statusOf)
    {
        var search = query.SearchText.Trim();
        var matches = new List<(ApplicationEntry Application, List<FolderEntry> Folders, int Score)>();
        foreach (var application in environment.Applications.Where(application => PassesApplicationFilters(application, query)))
        {
            var applicationScore = BestScore(search, application.Application.DisplayName, application.Application.Id, application.Application.Owner, string.Join(' ', application.Application.Tags));
            var folders = application.Folders
                .Where(folder => query.FolderTypes.IsEmpty || query.FolderTypes.Contains(folder.Folder.Type.Key))
                .Where(folder => applicationScore is not null || FolderScore(search, folder) is not null)
                .ToList();
            if (folders.Count > 0 || (applicationScore is not null && query.FolderTypes.IsEmpty))
            {
                var folderScore = folders.Select(folder => FolderScore(search, folder) ?? 0).DefaultIfEmpty(0).Max();
                matches.Add((application, SortFolders(folders, query.Sort, statusOf), Math.Max(applicationScore ?? 0, folderScore)));
            }
        }

        var ordered = OrderApplications(matches, query, statusOf);
        var rows = new List<RowItem>();
        foreach (var (application, folders, _) in ordered)
        {
            var isExpanded = query.IsSearching || query.IsFiltered || expansion.ApplicationIds.Contains(application.Application.Id);
            rows.Add(new ApplicationRowItem(application, folders.Count, isExpanded));
            if (!isExpanded)
            {
                continue;
            }

            for (var index = 0; index < folders.Count; index++)
            {
                var folder = folders[index];
                var isFolderExpanded = expansion.FolderKeys.Contains(folder.Key);
                rows.Add(new FolderRowItem(folder, index + 1, folders.Count) { IsExpanded = isFolderExpanded });
                if (isFolderExpanded)
                {
                    AddFileRows(rows, folder, expansion.FilesOf(folder) ?? FolderFiles.Loading);
                }
            }
        }

        return new RowListing(rows, matches.Sum(match => match.Folders.Count), environment.Folders.Count);
    }

    private static void AddFileRows(List<RowItem> rows, FolderEntry folder, FolderFiles files)
    {
        switch (files.State)
        {
            case FolderFilesState.Loading:
                rows.Add(new FolderNoteRowItem(folder, FolderNote.Loading));
                return;
            case FolderFilesState.Failed:
                rows.Add(new FolderNoteRowItem(folder, FolderNote.Failed) { Reachability = files.Reachability });
                return;
        }

        if (files.NewestFirst.Count == 0)
        {
            rows.Add(new FolderNoteRowItem(folder, FolderNote.Empty));
            return;
        }

        var shown = files.NewestFirst.Take(MaxFilesPerFolder).ToList();
        rows.AddRange(shown.Select((file, index) => new FileRowItem(folder, file, index + 1, shown.Count)));
        if (files.MatchingFileCount > shown.Count)
        {
            rows.Add(new FolderNoteRowItem(folder, FolderNote.MoreFiles) { ShownFileCount = shown.Count, MatchingFileCount = files.MatchingFileCount });
        }
    }

    private static bool PassesApplicationFilters(ApplicationEntry application, RowQuery query)
    {
        var tagMatches = query.Tags.IsEmpty || application.Application.Tags.Any(query.Tags.Contains);
        var ownerMatches = query.Owners.IsEmpty || (application.Application.Owner is { } owner && query.Owners.Contains(owner));
        return tagMatches && ownerMatches;
    }

    private static int? FolderScore(string search, FolderEntry folder)
    {
        return BestScore(search, folder.Folder.Label, folder.Folder.Type.Key);
    }

    private static int? BestScore(string search, params string?[] fields)
    {
        if (search.Length == 0)
        {
            return 0;
        }

        return fields.OfType<string>().Select(field => FuzzyMatcher.Score(search, field)).Max();
    }

    private static List<FolderEntry> SortFolders(List<FolderEntry> folders, RowSort sort, Func<FolderEntry, FolderStatus> statusOf)
    {
        return sort switch
        {
            RowSort.Type => [.. folders.OrderBy(folder => folder.Folder.Type.Key, StringComparer.Ordinal).ThenBy(folder => folder.Folder.Label, StringComparer.CurrentCultureIgnoreCase)],
            RowSort.Status => [.. folders.OrderBy(folder => StatusRank(statusOf(folder).Reachability)).ThenBy(folder => folder.Folder.Label, StringComparer.CurrentCultureIgnoreCase)],
            _ => folders,
        };
    }

    private static IEnumerable<(ApplicationEntry Application, List<FolderEntry> Folders, int Score)> OrderApplications(
        List<(ApplicationEntry Application, List<FolderEntry> Folders, int Score)> matches,
        RowQuery query,
        Func<FolderEntry, FolderStatus> statusOf)
    {
        if (query.IsSearching)
        {
            return matches.OrderByDescending(match => match.Score).ThenBy(match => match.Application.Application.DisplayName, StringComparer.CurrentCultureIgnoreCase);
        }

        return query.Sort == RowSort.Status
            ? matches.OrderBy(match => match.Folders.Select(folder => StatusRank(statusOf(folder).Reachability)).DefaultIfEmpty(int.MaxValue).Min())
                .ThenBy(match => match.Application.Application.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            : matches.OrderBy(match => match.Application.Application.DisplayName, StringComparer.CurrentCultureIgnoreCase);
    }

    private static int StatusRank(Reachability reachability)
    {
        return reachability switch
        {
            Reachability.Timeout => 0,
            Reachability.Denied => 1,
            Reachability.NotFound => 2,
            Reachability.Unknown => 3,
            Reachability.Checking => 4,
            _ => 5,
        };
    }
}
