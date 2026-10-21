using System.ComponentModel;
using System.Diagnostics;

namespace Jbx.Platform.Windows.Shell;

public sealed class WindowsShell : IShell
{
    private static readonly HashSet<string> ProgramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta",
        ".msi", ".msp", ".scr", ".pif", ".lnk", ".url", ".cpl", ".reg", ".jar", ".appref-ms", ".application",
    };

    // A full path, so an explorer.exe planted earlier on PATH or in the working folder is never the one started.
    private readonly string _explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    public Task<bool> OpenFolderAsync(CanonicalPath folder)
    {
        return StartAsync(ExplorerStart(folder.Display));
    }

    public Task<bool> RevealInFolderAsync(CanonicalPath file)
    {
        return StartAsync(ExplorerStart("/select,", file.Display));
    }

    public Task<bool> OpenFileAsync(CanonicalPath file)
    {
        if (IsProgramFile(file))
        {
            return Task.FromResult(false);
        }

        return StartAsync(new ProcessStartInfo(file.Display) { UseShellExecute = true });
    }

    public bool IsProgramFile(CanonicalPath file)
    {
        return ProgramExtensions.Contains(Path.GetExtension(file.Display));
    }

    private ProcessStartInfo ExplorerStart(params string[] arguments)
    {
        var start = new ProcessStartInfo(_explorer) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }

    private static Task<bool> StartAsync(ProcessStartInfo start)
    {
        return Task.Run(() =>
        {
            try
            {
                using var process = Process.Start(start);
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        });
    }
}
