using System.Globalization;
using Jbx.Desktop.Resources;

namespace Jbx.Desktop.Text;

internal static class RelativeTime
{
    public static string Describe(DateTimeOffset moment, DateTimeOffset now)
    {
        var elapsed = now - moment;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return Strings.Time_JustNow;
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return Strings.FormatTime_MinutesAgo((int)elapsed.TotalMinutes);
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return Strings.FormatTime_HoursAgo((int)elapsed.TotalHours);
        }

        return Absolute(moment);
    }

    public static string Absolute(DateTimeOffset moment)
    {
        return moment.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    public static string Size(long bytes)
    {
        const double Kilobyte = 1024;
        return bytes switch
        {
            < 1024 => Strings.FormatSize_Bytes(bytes),
            < 1024 * 1024 => Strings.FormatSize_Kilobytes((bytes / Kilobyte).ToString("0.#", CultureInfo.CurrentCulture)),
            _ => Strings.FormatSize_Megabytes((bytes / Kilobyte / Kilobyte).ToString("0.#", CultureInfo.CurrentCulture)),
        };
    }
}
