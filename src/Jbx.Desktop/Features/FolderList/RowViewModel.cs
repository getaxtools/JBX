using CommunityToolkit.Mvvm.ComponentModel;

namespace Jbx.Desktop.Features.FolderList;

internal abstract partial class RowViewModel : ObservableObject
{
    public abstract string Key { get; }

    public abstract string Name { get; }

    /// <summary>Level and position for screen readers, which Avalonia does not pass to Windows on its own.</summary>
    public abstract string AccessibleDescription { get; }

    public override string ToString()
    {
        return Name;
    }
}
