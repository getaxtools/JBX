using Jbx.Desktop.Infrastructure;
using Jbx.Platform;
using Jbx.Services.Logs;

namespace Jbx.Desktop.Features.LogViewer;

/// <summary>
/// The text of recently shown lines, read a page at a time and kept for a while. Only the UI thread
/// touches it; reads run in the background and report back through the dispatcher.
/// </summary>
internal sealed class LinePages(IUiDispatcher dispatcher) : IDisposable
{
    public const int PageSize = 200;
    public const int MaxPages = 60;

    private readonly Dictionary<int, IReadOnlyList<LogLine>> _pages = [];
    private readonly LinkedList<int> _useOrder = new();
    private readonly Dictionary<int, LinkedListNode<int>> _useNodes = [];
    private readonly Dictionary<int, int> _versions = [];
    private readonly HashSet<int> _loading = [];
    private readonly HashSet<int> _failed = [];
    private LogDocument? _document;
    private CancellationTokenSource _reads = new();
    private bool _isDisposed;

    /// <summary>A page arrived; its first line number is the argument.</summary>
    public event Action<int>? PageLoaded;

    public event Action<Reachability>? ReadFailed;

    public void Attach(LogDocument document)
    {
        Clear();
        _document = document;
    }

    public LogLine? TryGet(int number)
    {
        var page = number / PageSize;
        if (_pages.TryGetValue(page, out var lines))
        {
            var offset = number - (page * PageSize);
            if (offset < lines.Count)
            {
                return lines[offset];
            }

            Forget(page);
        }

        Request(page);
        return null;
    }

    /// <summary>
    /// Forgets the page holding a line that changed, such as a last line still being written. A read of
    /// that page already on its way is ignored when it lands, because it may predate the change.
    /// </summary>
    public void Invalidate(int number)
    {
        var page = number / PageSize;
        Forget(page);
        _versions[page] = VersionOf(page) + 1;
        _loading.Remove(page);
    }

    /// <summary>Pages that failed are not asked for again on every redraw; this asks once more.</summary>
    public void RetryFailed()
    {
        _failed.Clear();
    }

    public void Dispose()
    {
        _isDisposed = true;
        _reads.Cancel();
        _reads.Dispose();
    }

    public void Clear()
    {
        if (_isDisposed)
        {
            return;
        }

        _reads.Cancel();
        _reads.Dispose();
        _reads = new CancellationTokenSource();
        _pages.Clear();
        _useOrder.Clear();
        _useNodes.Clear();
        _versions.Clear();
        _loading.Clear();
        _failed.Clear();
    }

    private void Request(int page)
    {
        if (!_isDisposed && _document is { } document && !_failed.Contains(page) && _loading.Add(page))
        {
            _ = LoadAsync(document, new PageRequest(page, VersionOf(page)), _reads.Token);
        }
    }

    private async Task LoadAsync(LogDocument document, PageRequest request, CancellationToken cancellationToken)
    {
        // Requests come from the list asking for a row; answering inside that call would change the list mid-layout.
        await Task.Yield();
        LogPage result;
        try
        {
            result = await document.ReadLinesAsync(request.Page * PageSize, PageSize, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        dispatcher.Post(() => Store(request, result, cancellationToken));
    }

    private void Store(PageRequest request, LogPage result, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || request.Version != VersionOf(request.Page))
        {
            return;
        }

        _loading.Remove(request.Page);
        if (result.Reachability != Reachability.Reachable)
        {
            _failed.Add(request.Page);
            ReadFailed?.Invoke(result.Reachability);
            return;
        }

        Forget(request.Page);
        _pages[request.Page] = result.Lines;
        _useNodes[request.Page] = _useOrder.AddLast(request.Page);
        while (_pages.Count > MaxPages && _useOrder.First is { } oldest)
        {
            Forget(oldest.Value);
        }

        PageLoaded?.Invoke(request.Page * PageSize);
    }

    private void Forget(int page)
    {
        _pages.Remove(page);
        if (_useNodes.Remove(page, out var node))
        {
            _useOrder.Remove(node);
        }
    }

    private int VersionOf(int page)
    {
        return _versions.GetValueOrDefault(page);
    }

    private readonly record struct PageRequest(int Page, int Version);
}
