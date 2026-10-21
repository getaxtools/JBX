namespace Jbx.Platform;

/// <summary>The system's high-contrast colours while a contrast theme is on; null otherwise.</summary>
public interface ISystemContrast
{
    ContrastColors? Current { get; }
}
