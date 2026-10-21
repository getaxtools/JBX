using System.Globalization;
using System.Text;
using Jbx.Core.Config.Parsing;

namespace Jbx.Core.Config;

public static class ConfigParser
{
    public const long MaxConfigSizeBytes = 8_000_000;

    public static ConfigLoadResult Parse(ConfigSource source)
    {
        var context = new ProblemContext([], []);
        if (source.Content.Length > MaxConfigSizeBytes)
        {
            context.AddError(ConfigErrorCode.ConfigTooLarge, MaxConfigSizeBytes.ToString(CultureInfo.InvariantCulture), null);
            return new ConfigLoadResult(null, context.Errors, context.Warnings);
        }

        var text = Decode(source.Content);
        var root = DetectFormat(source.FileName, text) == ConfigFormat.Json
            ? JsonConfigReader.Read(text, context.Errors)
            : YamlConfigReader.Read(text, context.Errors);
        var document = root is null ? null : ConfigDocumentMapper.Map(root, context);
        var warningsInFileOrder = context.Warnings
            .OrderBy(warning => warning.Location?.Line ?? int.MaxValue)
            .ThenBy(warning => warning.Location?.Column ?? int.MaxValue)
            .ToList();
        return new ConfigLoadResult(document, context.Errors, warningsInFileOrder);
    }

    public static ConfigFormat DetectFormat(string fileName, string text)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigFormat.Json;
        }

        if (extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigFormat.Yaml;
        }

        return text.TrimStart().StartsWith('{') ? ConfigFormat.Json : ConfigFormat.Yaml;
    }

    private static string Decode(ReadOnlyMemory<byte> content)
    {
        using var reader = new StreamReader(new MemoryStream(content.ToArray()), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
