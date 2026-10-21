namespace Jbx.Desktop.Infrastructure;

/// <summary>The generated third-party notices, built into the program so the licences travel with the single file.</summary>
internal static class ThirdPartyNotices
{
    public const string ResourceName = "ThirdPartyNotices.txt";

    public static string Read()
    {
        using var stream = typeof(ThirdPartyNotices).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
