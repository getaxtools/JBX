using System.Diagnostics;

namespace Jbx.Testing;

/// <summary>Waits for background continuations to settle, failing loudly instead of sleeping a fixed time.</summary>
public static class Eventually
{
    public static async Task TrueAsync(Func<bool> condition, TimeSpan? within = null)
    {
        var limit = within ?? TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > limit)
            {
                throw new TimeoutException($"Condition still false after {limit}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5)).ConfigureAwait(false);
        }
    }
}
