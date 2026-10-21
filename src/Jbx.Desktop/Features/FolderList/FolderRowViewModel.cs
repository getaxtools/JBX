using CommunityToolkit.Mvvm.ComponentModel;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Platform;
using Jbx.Services.Rows;

namespace Jbx.Desktop.Features.FolderList;

internal sealed partial class FolderRowViewModel(FolderEntry entry, IRowActions actions) : RowViewModel
{
    public FolderEntry Entry { get; } = entry;

    public IRowActions Actions { get; } = actions;

    public override string Key => "folder:" + Entry.Key;

    public override string Name => Entry.Folder.Label;

    public string TypeText => Entry.Folder.Type.Key;

    public string PathText => Entry.Path?.Display ?? Strings.Rows_PathUnresolved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLabel), nameof(AccessibleDescription))]
    public partial Reachability Status { get; set; } = Reachability.Unknown;

    [ObservableProperty]
    public partial DateTimeOffset? CheckedAt { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleDescription))]
    public partial bool IsFavourite { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText), nameof(AccessibleDescription))]
    public partial bool IsExpanded { get; set; }

    public string ToggleText => IsExpanded ? Strings.FormatRows_CollapseNode(Name) : Strings.FormatRows_ExpandNode(Name);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleDescription))]
    public partial int Position { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleDescription))]
    public partial int SiblingCount { get; set; }

    public string StatusLabel => StatusText.Label(Status);

    public override string AccessibleDescription =>
        string.Join(", ", new[]
        {
            IsExpanded ? Strings.Rows_Expanded : Strings.Rows_Collapsed,
            TypeText,
            StatusLabel,
            IsFavourite ? Strings.Rows_Favourite : null,
            Strings.FormatRows_LevelAndPosition(2, Position, SiblingCount),
        }.OfType<string>());
}
