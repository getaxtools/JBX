using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Jbx.Platform.Windows.FileSystem;

public sealed partial class NetworkDriveResolver : IMappedDriveResolver
{
    private const int NoError = 0;
    private const int MaxRemoteNameLength = 1024;

    private readonly ConcurrentDictionary<char, string?> _resolved = new();

    public string? TryResolve(char driveLetter)
    {
        return _resolved.GetOrAdd(char.ToUpperInvariant(driveLetter), Query);
    }

    private static string? Query(char driveLetter)
    {
        var buffer = new char[MaxRemoteNameLength];
        var length = buffer.Length;
        var result = WNetGetConnection($"{driveLetter}:", buffer, ref length);
        return result == NoError ? new string(buffer).TrimEnd('\0') : null;
    }

    [LibraryImport("mpr.dll", EntryPoint = "WNetGetConnectionW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int WNetGetConnection(string localName, [Out] char[] remoteName, ref int length);
}
