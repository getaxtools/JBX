using System.Globalization;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Text;

internal static class Counts
{
    public static string Folders(int count)
    {
        return count == 1 ? Strings.Rows_FolderCountOne : Strings.FormatRows_FolderCount(count);
    }

    public static string Problems(int count)
    {
        return count == 1 ? Strings.Rows_ProblemCountOne : Strings.FormatRows_ProblemCount(count);
    }

    /// <summary>A count as the reader expects it, with thousands separators.</summary>
    public static string Number(long count)
    {
        return count.ToString("N0", CultureInfo.CurrentCulture);
    }
}
