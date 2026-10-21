namespace Jbx.Services.Settings;

public sealed record SessionPlan(IReadOnlyList<RestoredTab> Tabs, int SelectedTab)
{
    public static SessionPlan Nothing { get; } = new([], 0);

    public bool IsEmpty => Tabs.Count == 0;
}
