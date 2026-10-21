using System.Collections.ObjectModel;
using Jbx.Desktop.Features.FolderList;

namespace Jbx.Desktop.Features.Tabs;

/// <summary>
/// Updates the bound row collection to a new order with the fewest changes, so selection and scroll
/// position survive expanding, searching and status updates.
/// </summary>
internal static class RowDiff
{
    public static void Apply(ObservableCollection<RowViewModel> current, IReadOnlyList<RowViewModel> wanted)
    {
        var wantedSet = new HashSet<RowViewModel>(wanted);
        for (var index = current.Count - 1; index >= 0; index--)
        {
            if (!wantedSet.Contains(current[index]))
            {
                current.RemoveAt(index);
            }
        }

        for (var index = 0; index < wanted.Count; index++)
        {
            if (index < current.Count && ReferenceEquals(current[index], wanted[index]))
            {
                continue;
            }

            var existing = current.IndexOf(wanted[index]);
            if (existing >= 0)
            {
                current.Move(existing, index);
            }
            else
            {
                current.Insert(index, wanted[index]);
            }
        }
    }
}
