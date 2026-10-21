using Jbx.Desktop.Resources;
using Jbx.Platform;

namespace Jbx.Desktop.Text;

/// <summary>The one status vocabulary, used by every badge, the details panel and announcements.</summary>
internal static class StatusText
{
    public static string Label(Reachability reachability)
    {
        return reachability switch
        {
            Reachability.Reachable => Strings.Status_Reachable,
            Reachability.NotFound => Strings.Status_NotFound,
            Reachability.Denied => Strings.Status_Denied,
            Reachability.Timeout => Strings.Status_Timeout,
            Reachability.Checking => Strings.Status_Checking,
            _ => Strings.Status_Unknown,
        };
    }

    public static string Remedy(Reachability reachability)
    {
        return reachability switch
        {
            Reachability.Reachable => Strings.Remedy_Reachable,
            Reachability.NotFound => Strings.Remedy_NotFound,
            Reachability.Denied => Strings.Remedy_Denied,
            Reachability.Timeout => Strings.Remedy_Timeout,
            Reachability.Checking => Strings.Remedy_Checking,
            _ => Strings.Remedy_Unknown,
        };
    }

    public static bool IsProblem(Reachability reachability)
    {
        return reachability is Reachability.NotFound or Reachability.Denied or Reachability.Timeout or Reachability.Unknown;
    }
}
