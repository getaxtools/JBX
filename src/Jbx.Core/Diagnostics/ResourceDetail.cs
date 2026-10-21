namespace Jbx.Core.Diagnostics;

/// <summary>Paths, hosts and other machine-specific values; written to the local log only, never reported.</summary>
public sealed record ResourceDetail(IReadOnlyList<ResourceItem> Items)
{
    public ResourceDetail(params ResourceItem[] items)
        : this((IReadOnlyList<ResourceItem>)items)
    {
    }
}
