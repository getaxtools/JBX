using System.Collections;
using System.Collections.Specialized;

namespace Jbx.Desktop.Features.LogViewer;

/// <summary>
/// A list of millions of lines that holds none of them. The list control asks only for the rows on
/// screen, by index, and this asks the viewer for each one. It must stay an <see cref="IList"/>:
/// given a plain enumerable, Avalonia would copy every item.
/// </summary>
internal sealed class VirtualLines(Func<int, LogLineViewModel> lineAt, Func<LogLineViewModel, int> indexOf) : IList, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count { get; private set; }

    public bool IsFixedSize => false;

    public bool IsReadOnly => true;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    public object? this[int index]
    {
        get => index >= 0 && index < Count ? lineAt(index) : null;
        set => throw new NotSupportedException();
    }

    /// <summary>Lines were added at the end, as when a followed log grows.</summary>
    public void GrowTo(int count)
    {
        if (count <= Count)
        {
            return;
        }

        var start = Count;
        Count = count;
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new Blank(count - start), start));
    }

    /// <summary>The list became a different list, as when a filter changes.</summary>
    public void Reset(int count)
    {
        Count = count;
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void Replace(int index, LogLineViewModel previous, LogLineViewModel current)
    {
        if (index < Count)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, current, previous, index));
        }
    }

    public int IndexOf(object? value)
    {
        return value is LogLineViewModel line ? indexOf(line) : -1;
    }

    public bool Contains(object? value)
    {
        return IndexOf(value) >= 0;
    }

    public IEnumerator GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return lineAt(index);
        }
    }

    public void CopyTo(Array array, int index)
    {
        throw new NotSupportedException();
    }

    public int Add(object? value)
    {
        throw new NotSupportedException();
    }

    public void Clear()
    {
        throw new NotSupportedException();
    }

    public void Insert(int index, object? value)
    {
        throw new NotSupportedException();
    }

    public void Remove(object? value)
    {
        throw new NotSupportedException();
    }

    public void RemoveAt(int index)
    {
        throw new NotSupportedException();
    }

    /// <summary>Stands for the added rows in a change notification without creating them.</summary>
    private sealed class Blank(int count) : IList
    {
        public int Count { get; } = count;

        public bool IsFixedSize => true;

        public bool IsReadOnly => true;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public object? this[int index]
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public int IndexOf(object? value)
        {
            return -1;
        }

        public bool Contains(object? value)
        {
            return false;
        }

        public IEnumerator GetEnumerator()
        {
            return Enumerable.Repeat<object?>(null, Count).GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotSupportedException();
        }

        public int Add(object? value)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public void Insert(int index, object? value)
        {
            throw new NotSupportedException();
        }

        public void Remove(object? value)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }
    }
}
