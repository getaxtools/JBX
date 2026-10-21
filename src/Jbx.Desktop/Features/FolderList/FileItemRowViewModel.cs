using CommunityToolkit.Mvvm.ComponentModel;
using Jbx.Desktop.Resources;
using Jbx.Desktop.Text;
using Jbx.Platform;
using Jbx.Services.Rows;

namespace Jbx.Desktop.Features.FolderList;

internal sealed partial class FileItemRowViewModel(FolderEntry folder, FileEntry file, IRowActions actions) : RowViewModel
{
    public FolderEntry Folder { get; } = folder;

    public IRowActions Actions { get; } = actions;

    public override string Key => $"file:{Folder.Key}|{File.Path.Key}";

    public override string Name => File.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SizeText), nameof(LastWriteText), nameof(LastWriteExact), nameof(AccessibleDescription))]
    public partial FileEntry File { get; set; } = file;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastWriteText))]
    public partial DateTimeOffset Now { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleDescription))]
    public partial bool IsFavourite { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleDescription))]
    public partial int Position { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleDescription))]
    public partial int SiblingCount { get; set; }

    public string SizeText => RelativeTime.Size(File.SizeBytes);

    public string LastWriteText => RelativeTime.Describe(File.LastWriteUtc, Now);

    public string LastWriteExact => RelativeTime.Absolute(File.LastWriteUtc);

    public override string AccessibleDescription =>
        string.Join(", ", new[]
        {
            LastWriteExact,
            SizeText,
            IsFavourite ? Strings.Rows_Favourite : null,
            Strings.FormatRows_LevelAndPosition(3, Position, SiblingCount),
        }.OfType<string>());
}
