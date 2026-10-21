namespace Jbx.Core.Config;

/// <summary>Parse then validate, collecting every problem from both steps into one result.</summary>
public sealed class ConfigLoader(ConfigValidator validator)
{
    public ConfigLoadResult Load(ConfigSource source)
    {
        var parsed = ConfigParser.Parse(source);
        if (parsed.Document is null)
        {
            return parsed;
        }

        var errors = parsed.Errors.Concat(validator.Validate(parsed.Document)).ToList();
        return parsed with { Errors = errors };
    }
}
