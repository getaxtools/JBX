using Jbx.Desktop.Text;
using Jbx.Platform;

namespace Jbx.Desktop.Features.Details;

internal sealed class FileRowViewModel(FileEntry entry, DateTimeOffset now)
{
    public FileEntry Entry { get; } = entry;

    public string Name => Entry.Name;

    public string SizeText => RelativeTime.Size(Entry.SizeBytes);

    public string LastWriteText => RelativeTime.Describe(Entry.LastWriteUtc, now);

    public string LastWriteExact => RelativeTime.Absolute(Entry.LastWriteUtc);

    public override string ToString()
    {
        return Name;
    }
}
