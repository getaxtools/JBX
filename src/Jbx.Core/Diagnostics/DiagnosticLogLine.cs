using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Jbx.Core.Diagnostics;

public static class DiagnosticLogLine
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    public static string Format(DateTimeOffset timestamp, AppEvent appEvent, ResourceDetail? detail)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("time", timestamp.ToUniversalTime());
            writer.WriteString("severity", appEvent.Severity.ToString());
            writer.WriteString("event", appEvent.Code.ToString());
            WriteMeasures(writer, appEvent.Measures);
            if (detail is not null)
            {
                WriteDetail(writer, detail);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static void WriteMeasures(Utf8JsonWriter writer, IReadOnlyList<EventMeasure> measures)
    {
        writer.WriteStartArray("measures");
        foreach (var measure in measures)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", measure.Kind.ToString());
            writer.WriteNumber("value", measure.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteDetail(Utf8JsonWriter writer, ResourceDetail detail)
    {
        writer.WriteStartArray("detail");
        foreach (var item in detail.Items)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", item.Kind.ToString());
            writer.WriteString("value", item.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
