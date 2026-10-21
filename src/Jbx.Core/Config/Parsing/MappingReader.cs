using System.Globalization;

namespace Jbx.Core.Config.Parsing;

/// <summary>Typed access to one mapping that reports missing, mistyped, duplicate and unknown keys.</summary>
internal sealed class MappingReader
{
    private readonly ConfigMapping _mapping;
    private readonly ProblemContext _context;
    private readonly Dictionary<string, ConfigMappingEntry> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _readKeys = new(StringComparer.Ordinal);

    public MappingReader(ConfigMapping mapping, ProblemContext context)
    {
        _mapping = mapping;
        _context = context;
        foreach (var entry in mapping.Entries)
        {
            if (!_entries.TryAdd(entry.Key, entry))
            {
                context.AddError(ConfigErrorCode.DuplicateKey, entry.Key, entry.KeyLocation ?? mapping.Location);
            }
        }
    }

    public SourceLocation? Location => _mapping.Location;

    public string RequiredText(string key)
    {
        var text = OptionalText(key);
        if (text is null && !_entries.ContainsKey(key))
        {
            _context.AddError(ConfigErrorCode.MissingField, key, _mapping.Location);
        }

        return text ?? string.Empty;
    }

    public string? OptionalText(string key)
    {
        if (!TryGet(key, out var node) || node is ConfigScalar { IsNull: true })
        {
            return null;
        }

        if (node is ConfigScalar scalar)
        {
            return scalar.Value;
        }

        _context.AddError(ConfigErrorCode.ExpectedText, key, node.Location ?? _mapping.Location);
        return null;
    }

    public bool? OptionalBoolean(string key)
    {
        var text = OptionalText(key);
        if (text is null)
        {
            return null;
        }

        if (bool.TryParse(text, out var value))
        {
            return value;
        }

        _context.AddError(ConfigErrorCode.InvalidBoolean, key, LocationOf(key));
        return null;
    }

    public int? OptionalInteger(string key)
    {
        var text = OptionalText(key);
        if (text is null)
        {
            return null;
        }

        if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        _context.AddError(ConfigErrorCode.InvalidNumber, key, LocationOf(key));
        return null;
    }

    public IReadOnlyList<ConfigNode> Sequence(string key)
    {
        if (!TryGet(key, out var node) || node is ConfigScalar { IsNull: true })
        {
            return [];
        }

        if (node is ConfigSequence sequence)
        {
            return sequence.Items;
        }

        _context.AddError(ConfigErrorCode.ExpectedSequence, key, node.Location ?? _mapping.Location);
        return [];
    }

    public ConfigMapping? OptionalMapping(string key)
    {
        if (!TryGet(key, out var node) || node is ConfigScalar { IsNull: true })
        {
            return null;
        }

        if (node is ConfigMapping mapping)
        {
            return mapping;
        }

        _context.AddError(ConfigErrorCode.ExpectedMapping, key, node.Location ?? _mapping.Location);
        return null;
    }

    public void ReportUnknownKeys()
    {
        foreach (var entry in _mapping.Entries.Where(entry => !_readKeys.Contains(entry.Key)))
        {
            _context.AddWarning(ConfigErrorCode.UnknownKey, entry.Key, entry.KeyLocation ?? _mapping.Location);
        }
    }

    private bool TryGet(string key, out ConfigNode node)
    {
        _readKeys.Add(key);
        if (_entries.TryGetValue(key, out var entry))
        {
            node = entry.Value;
            return true;
        }

        node = new ConfigScalar(string.Empty, IsNull: true, null);
        return false;
    }

    private SourceLocation? LocationOf(string key)
    {
        return _entries.TryGetValue(key, out var entry) ? entry.Value.Location ?? _mapping.Location : _mapping.Location;
    }
}
