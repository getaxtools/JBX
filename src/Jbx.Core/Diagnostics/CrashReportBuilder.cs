using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Jbx.Core.Diagnostics;

/// <summary>
/// Builds a crash report from an allowlist: exception types, error codes and our own method names.
/// Exception messages are never included, because they routinely contain paths, hosts and user names.
/// </summary>
public static class CrashReportBuilder
{
    private const int MaxInnerExceptions = 8;
    private const int MaxFramesPerException = 40;

    public static string Build(Exception exception, string appVersion, DateTimeOffset timestamp)
    {
        var report = new StringBuilder();
        report.AppendLine("JBX crash report");
        report.AppendLine(CultureInfo.InvariantCulture, $"Version: {appVersion}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Runtime: {RuntimeInformation.FrameworkDescription}");
        report.AppendLine(CultureInfo.InvariantCulture, $"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        report.AppendLine(CultureInfo.InvariantCulture, $"Time (UTC): {timestamp.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}");
        report.AppendLine(CultureInfo.InvariantCulture, $"Report id: {Guid.NewGuid():N}");

        var current = exception;
        for (var depth = 0; current is not null && depth <= MaxInnerExceptions; depth++)
        {
            report.AppendLine();
            report.AppendLine(CultureInfo.InvariantCulture, $"{(depth == 0 ? "Exception" : "Caused by")}: {current.GetType().FullName} (HResult 0x{current.HResult:X8})");
            AppendFrames(report, current);
            current = current.InnerException;
        }

        return report.ToString();
    }

    private static void AppendFrames(StringBuilder report, Exception exception)
    {
        string? previousExternal = null;
        var frames = new StackTrace(exception, fNeedFileInfo: false).GetFrames();
        foreach (var frame in frames.Take(MaxFramesPerException))
        {
            var line = DescribeFrame(frame);
            if (line.StartsWith('[') && line == previousExternal)
            {
                continue;
            }

            previousExternal = line.StartsWith('[') ? line : null;
            report.AppendLine(CultureInfo.InvariantCulture, $"  at {line}");
        }
    }

    // DiagnosticMethodInfo rather than StackFrame.GetMethod: it survives trimming of the published program.
    private static string DescribeFrame(StackFrame frame)
    {
        var method = DiagnosticMethodInfo.Create(frame);
        if (method is null)
        {
            return "[unknown]";
        }

        var assembly = method.DeclaringAssemblyName is { } fullName ? new AssemblyName(fullName).Name ?? "unknown" : "unknown";
        var isOurs = assembly == "JBX" || assembly.StartsWith("Jbx.", StringComparison.Ordinal);
        return isOurs ? $"{method.DeclaringTypeName}.{method.Name}" : $"[{assembly}]";
    }
}
