using Jbx.Desktop.Resources;
using Jbx.Services.ConfigSession;

namespace Jbx.Desktop.Text;

internal static class ConfigOpenText
{
    public static string Describe(ConfigOpenOutcome outcome)
    {
        var path = outcome.PathText ?? string.Empty;
        return outcome.Status switch
        {
            ConfigOpenStatus.InvalidPath => Strings.FormatConfigOpen_InvalidPath(path),
            ConfigOpenStatus.NotFound => Strings.FormatConfigOpen_NotFound(path),
            ConfigOpenStatus.Denied => Strings.FormatConfigOpen_Denied(path),
            ConfigOpenStatus.Timeout => Strings.FormatConfigOpen_Timeout(path),
            ConfigOpenStatus.TooLarge => Strings.FormatConfigOpen_TooLarge(path),
            ConfigOpenStatus.Invalid => Strings.FormatConfigOpen_Invalid(path, outcome.Load?.Errors.Count ?? 0),
            _ => Strings.FormatConfigOpen_Unreadable(path),
        };
    }
}
