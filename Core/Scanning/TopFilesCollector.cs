using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Scanning;

/// <summary>
/// Thread-safe, bounded memory priority heap for tracking the top largest junk files
/// without unbounded heap allocations during massive directory scans.
/// </summary>
public sealed class TopFilesCollector
{
    private readonly int _capacity;
    private readonly PriorityQueue<JunkFileItem, long> _minHeap;
    private readonly object _syncLock = new();

    public TopFilesCollector(int capacity = 30)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
        _capacity = capacity;
        _minHeap = new PriorityQueue<JunkFileItem, long>(capacity);
    }

    public int Capacity => _capacity;

    public int Count
    {
        get
        {
            lock (_syncLock)
            {
                return _minHeap.Count;
            }
        }
    }

    /// <summary>
    /// Evaluates a candidate file and retains it if it qualifies among the top N largest items.
    /// </summary>
    public bool TryAdd(JunkFileItem item)
    {
        if (item == null || item.SizeBytes <= 0) return false;

        lock (_syncLock)
        {
            if (_minHeap.Count < _capacity)
            {
                _minHeap.Enqueue(item, item.SizeBytes);
                return true;
            }

            // In min-heap, Peek() returns the item with the smallest SizeBytes
            if (_minHeap.TryPeek(out _, out long minSize) && item.SizeBytes > minSize)
            {
                _minHeap.Dequeue();
                _minHeap.Enqueue(item, item.SizeBytes);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Convenience overload to construct and record candidate file details.
    /// </summary>
    public bool TryAdd(string fileName, string filePath, long sizeBytes, DateTime lastModified)
    {
        if (sizeBytes <= 0) return false;

        lock (_syncLock)
        {
            if (_minHeap.Count < _capacity || (_minHeap.TryPeek(out _, out long minSize) && sizeBytes > minSize))
            {
                var item = new JunkFileItem
                {
                    FileName = fileName,
                    FilePath = filePath,
                    SizeBytes = sizeBytes,
                    LastModified = lastModified
                };

                if (_minHeap.Count < _capacity)
                {
                    _minHeap.Enqueue(item, sizeBytes);
                }
                else
                {
                    _minHeap.Dequeue();
                    _minHeap.Enqueue(item, sizeBytes);
                }
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Returns the accumulated top files sorted descending by size.
    /// </summary>
    public List<JunkFileItem> ToDescendingList(int? take = null)
    {
        List<JunkFileItem> snapshot;
        lock (_syncLock)
        {
            snapshot = _minHeap.UnorderedItems
                .Select(x => x.Element)
                .OrderByDescending(x => x.SizeBytes)
                .ToList();
        }

        return take.HasValue ? snapshot.Take(take.Value).ToList() : snapshot;
    }

    public void Clear()
    {
        lock (_syncLock)
        {
            _minHeap.Clear();
        }
    }
}
