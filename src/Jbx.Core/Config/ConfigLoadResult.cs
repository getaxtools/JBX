namespace Jbx.Core.Config;

/// <summary>
/// The document as far as it could be read, plus every problem found. Use the document only
/// when <see cref="HasErrors"/> is false; it is kept otherwise so validation can still run on it.
/// </summary>
public sealed record ConfigLoadResult(
    ConfigDocument? Document,
    IReadOnlyList<ConfigProblem> Errors,
    IReadOnlyList<ConfigProblem> Warnings)
{
    public bool HasErrors => Errors.Count > 0 || Document is null;
}
