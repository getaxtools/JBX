using Jbx.Platform;

namespace Jbx.Desktop.Features.LogViewer;

/// <summary>A file to open in the viewer, with the words that say where it came from.</summary>
internal sealed record LogTarget(CanonicalPath Path, string Title, string Subtitle);
