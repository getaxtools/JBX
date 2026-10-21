namespace Jbx.Services.Logs;

public sealed record LevelCounts(int Errors, int Warnings, int Info, int Debug)
{
    public static LevelCounts None { get; } = new(0, 0, 0, 0);

    public LevelCounts Plus(LevelCounts more)
    {
        return new LevelCounts(Errors + more.Errors, Warnings + more.Warnings, Info + more.Info, Debug + more.Debug);
    }
}
