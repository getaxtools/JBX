namespace Jbx.Platform;

/// <summary>
/// A path in two forms: <see cref="Display"/> as the user should see it, and <see cref="Key"/>
/// for comparison, caching and favourites. Created only by an <see cref="IPathCanonicalizer"/>.
/// </summary>
public sealed class CanonicalPath : IEquatable<CanonicalPath>
{
    public CanonicalPath(string display, string key)
    {
        Display = display;
        Key = key;
    }

    public string Display { get; }

    public string Key { get; }

    public static bool operator ==(CanonicalPath? left, CanonicalPath? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(CanonicalPath? left, CanonicalPath? right)
    {
        return !Equals(left, right);
    }

    public bool Equals(CanonicalPath? other)
    {
        return other is not null && string.Equals(Key, other.Key, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is CanonicalPath other && Equals(other);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(Key);
    }

    public override string ToString()
    {
        return Display;
    }
}
