namespace Jbx.Services.Probing;

/// <summary>
/// Bounds every file-system call by time and by concurrency. A timeout stops the waiting, not the call:
/// Windows cannot cancel a call to an unreachable share, so the worker keeps its slot until it really
/// finishes. That is what stops abandoned calls from exhausting threads.
/// </summary>
public sealed class FileSystemGate(TimeProvider timeProvider) : IDisposable
{
    public const int MaxConcurrentCalls = 16;

    private readonly SemaphoreSlim _slots = new(MaxConcurrentCalls, MaxConcurrentCalls);

    public async Task<GateResult<T>> RunAsync<T>(Func<Task<T>> call, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // The clock starts before queueing for a slot, so a call stuck behind blocked workers still times out.
        var deadline = Task.Delay(timeout, timeProvider, cancellationToken);
        var slot = _slots.WaitAsync(CancellationToken.None);
        if (await Task.WhenAny(slot, deadline).ConfigureAwait(false) != slot)
        {
            _ = slot.ContinueWith(_ => ReleaseSlot(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
            return new GateResult<T>(IsTimedOut: true, default);
        }

        var work = StartHoldingSlot(call);
        if (await Task.WhenAny(work, deadline).ConfigureAwait(false) == work)
        {
            return new GateResult<T>(IsTimedOut: false, await work.ConfigureAwait(false));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new GateResult<T>(IsTimedOut: true, default);
    }

    public void Dispose()
    {
        _slots.Dispose();
    }

    private Task<T> StartHoldingSlot<T>(Func<Task<T>> call)
    {
        Task<T> work;
        try
        {
            work = call();
        }
        catch
        {
            ReleaseSlot();
            throw;
        }

        _ = work.ContinueWith(
            finished =>
            {
                _ = finished.Exception;
                ReleaseSlot();
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
        return work;
    }

    private void ReleaseSlot()
    {
        // An abandoned call can finish after shutdown, when nothing is waiting for its slot any more.
        try
        {
            _slots.Release();
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }
}
