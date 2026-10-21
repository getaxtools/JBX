using CommunityToolkit.Mvvm.ComponentModel;
using Jbx.Services.Rows;

namespace Jbx.Desktop.Features.FolderList;

/// <summary>The line under an expanded folder that says it is loading, empty, failed or has more files.</summary>
internal sealed partial class FolderNoteRowViewModel(FolderEntry folder) : RowViewModel
{
    public FolderEntry Folder { get; } = folder;

    public override string Key => "note:" + Folder.Key;

    public override string Name => Text;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(AccessibleDescription))]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsProblem { get; set; }

    public override string AccessibleDescription => Text;
}
