namespace Jbx.Platform.Windows.FileSystem;

/// <summary>
/// Turns a Windows failure into a distinct reachability. Directory.Exists cannot do this: it answers
/// false for "access denied", which would send the user to fix a config that is correct.
/// </summary>
internal static class ReachabilityClassifier
{
    private static readonly int[] NotFoundCodes = [2, 3, 53, 67, 123, 161, 267];
    private static readonly int[] DeniedCodes = [5, 86, 1326, 1327, 1330, 1331, 1909];

    public static ProbeResult Classify(IOException exception)
    {
        if (exception is DirectoryNotFoundException or FileNotFoundException)
        {
            return new ProbeResult(Reachability.NotFound);
        }

        var errorCode = exception.HResult & 0xFFFF;
        if (NotFoundCodes.Contains(errorCode))
        {
            return new ProbeResult(Reachability.NotFound, errorCode);
        }

        return DeniedCodes.Contains(errorCode)
            ? new ProbeResult(Reachability.Denied, errorCode)
            : new ProbeResult(Reachability.Unknown, errorCode);
    }

    public static ProbeResult Denied()
    {
        return new ProbeResult(Reachability.Denied);
    }
}
