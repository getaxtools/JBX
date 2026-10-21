using System.Text.Json;
using Jbx.Core.Diagnostics;
using Xunit;

namespace Jbx.Core.Tests.Diagnostics;

public sealed class DiagnosticLogLineTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 20, 9, 30, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Writes_time_severity_event_measures_and_detail_as_one_json_object()
    {
        var appEvent = new AppEvent(
            EventCode.ProbeTimedOut,
            EventSeverity.Warning,
            new EventMeasure(MeasureKind.DurationMilliseconds, 3000));
        var detail = new ResourceDetail(new ResourceItem(ResourceKind.Path, @"\\fs01.contoso.test\logs\api"));

        var line = DiagnosticLogLine.Format(Timestamp, appEvent, detail);

        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        Assert.Equal("2026-10-20T07:30:00+00:00", root.GetProperty("time").GetString());
        Assert.Equal("Warning", root.GetProperty("severity").GetString());
        Assert.Equal("ProbeTimedOut", root.GetProperty("event").GetString());
        Assert.Equal(3000, root.GetProperty("measures")[0].GetProperty("value").GetInt64());
        Assert.Equal("DurationMilliseconds", root.GetProperty("measures")[0].GetProperty("kind").GetString());
        Assert.Equal(@"\\fs01.contoso.test\logs\api", root.GetProperty("detail")[0].GetProperty("value").GetString());
    }

    [Fact]
    public void Escapes_a_path_containing_a_line_break_so_it_cannot_forge_a_second_record()
    {
        var appEvent = new AppEvent(EventCode.ProbeFailed, EventSeverity.Error);
        var forged = "C:\\logs\n{\"event\":\"AppStarted\"}";
        var detail = new ResourceDetail(new ResourceItem(ResourceKind.Path, forged));

        var line = DiagnosticLogLine.Format(Timestamp, appEvent, detail);

        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        using var json = JsonDocument.Parse(line);
        Assert.Equal(forged, json.RootElement.GetProperty("detail")[0].GetProperty("value").GetString());
    }

    [Fact]
    public void Leaves_out_the_detail_section_when_an_event_has_none()
    {
        var appEvent = new AppEvent(EventCode.AppStarted, EventSeverity.Information);

        var line = DiagnosticLogLine.Format(Timestamp, appEvent, detail: null);

        using var json = JsonDocument.Parse(line);
        Assert.False(json.RootElement.TryGetProperty("detail", out _));
        Assert.Equal(0, json.RootElement.GetProperty("measures").GetArrayLength());
    }
}
