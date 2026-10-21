using Jbx.Platform;

namespace Jbx.Services.Settings;

public sealed record RestoredFile(CanonicalPath Path, string Title, string Subtitle);
