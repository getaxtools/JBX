using CommunityToolkit.Mvvm.ComponentModel;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Services.Rows;

namespace Jbx.Desktop.Features.FolderList;

internal sealed partial class ApplicationRowViewModel(ApplicationEntry entry, IRowActions actions) : RowViewModel
{
    public ApplicationEntry Entry { get; } = entry;

    public IRowActions Actions { get; } = actions;

    public override string Key => "app:" + Entry.Application.Id;

    public override string Name => Entry.Application.DisplayName;

    public string? Owner => Entry.Application.Owner;

    public string Id => Entry.Application.Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleText))]
    public partial bool IsExpanded { get; set; }

    public string ToggleText => IsExpanded ? Strings.FormatRows_CollapseNode(Name) : Strings.FormatRows_ExpandNode(Name);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FolderCountText), nameof(AccessibleDescription))]
    public partial int VisibleFolderCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblems), nameof(AccessibleDescription))]
    public partial string? ProblemSummary { get; set; }

    public bool HasProblems => ProblemSummary is not null;

    public string FolderCountText => Counts.Folders(VisibleFolderCount);

    public override string AccessibleDescription =>
        string.Join(", ", new[] { IsExpanded ? Strings.Rows_Expanded : Strings.Rows_Collapsed, FolderCountText, ProblemSummary }.OfType<string>());

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(AccessibleDescription));
    }
}
