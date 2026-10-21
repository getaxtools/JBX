using Jbx.Core.Diagnostics;
using Xunit;

namespace Jbx.Core.Tests.Diagnostics;

public sealed class CrashReportBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 1, 30, 0, TimeSpan.Zero);

    private static readonly string[] Sentinels =
    [
        "SENTINEL-HOST-fs01",
        "SENTINEL-SHARE-AppLogs",
        "SENTINEL-PATH-Billing",
        "SENTINEL-USER-asha.rao",
        "SENTINEL-MACHINE-JUMPBOX07",
        "SENTINEL-CONFIG-shareRoot",
        "SENTINEL-FILE-api-2026.log",
        "SENTINEL-ENVVAR-value",
    ];

    [Fact]
    public void Leaves_out_every_path_host_user_and_config_value_carried_by_the_exception()
    {
        var exception = Capture(() => ThrowWithSensitiveDetail());

        var report = CrashReportBuilder.Build(exception, "0.1.0", Now);

        Assert.All(Sentinels, sentinel => Assert.DoesNotContain(sentinel, report, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Names_the_exception_types_and_error_codes_of_the_whole_chain()
    {
        var exception = Capture(() => ThrowWithSensitiveDetail());

        var report = CrashReportBuilder.Build(exception, "0.1.0", Now);

        Assert.Contains("Exception: System.InvalidOperationException", report, StringComparison.Ordinal);
        Assert.Contains("Caused by: System.IO.IOException (HResult 0x80070035)", report, StringComparison.Ordinal);
        Assert.Contains("Version: 0.1.0", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_our_method_names_and_reduces_other_frames_to_their_assembly()
    {
        var exception = Capture(() => _ = Enumerable.Range(1, 1).Select(_ => ThrowFromOurCode()).ToList());

        var report = CrashReportBuilder.Build(exception, "0.1.0", Now);

        Assert.Contains(nameof(ThrowFromOurCode), report, StringComparison.Ordinal);
        Assert.Contains("[System.Linq]", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Gives_each_report_its_own_id_so_reports_cannot_be_linked_to_one_person()
    {
        var exception = Capture(() => ThrowFromOurCode());

        var first = CrashReportBuilder.Build(exception, "0.1.0", Now);
        var second = CrashReportBuilder.Build(exception, "0.1.0", Now);

        Assert.NotEqual(IdOf(first), IdOf(second));
    }

    private static void ThrowWithSensitiveDetail()
    {
        try
        {
            throw new IOException(@"The network path \\SENTINEL-HOST-fs01\SENTINEL-SHARE-AppLogs\SENTINEL-PATH-Billing was not found (SENTINEL-USER-asha.rao on SENTINEL-MACHINE-JUMPBOX07)", unchecked((int)0x80070035));
        }
        catch (IOException inner)
        {
            var outer = new InvalidOperationException("Resolving {{SENTINEL-CONFIG-shareRoot}} for SENTINEL-FILE-api-2026.log with SENTINEL-ENVVAR-value failed", inner);
            outer.Data["path"] = "SENTINEL-PATH-Billing";
            throw outer;
        }
    }

    private static int ThrowFromOurCode()
    {
        throw new InvalidOperationException("SENTINEL-PATH-Billing");
    }

    private static InvalidOperationException Capture(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected an exception.");
    }

    private static string IdOf(string report)
    {
        return report.Split('\n').Single(line => line.StartsWith("Report id:", StringComparison.Ordinal));
    }
}
