namespace CodingTask;

/// <summary>
/// In-memory LRU cache with absolute TTL.
/// This version is not thread-safe; concurrency is a separate exercise.
/// </summary>
public sealed class CacheStorage
{
    private readonly object _syncRoot = new();
    private readonly int _cacheSize;
    private readonly Dictionary<long, Entry> _storage;
    private readonly LinkedList<long> _lru = new();

    private sealed record Entry(CacheItem Item, LinkedListNode<long> Node);

    public CacheStorage(int cacheSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cacheSize);
        _cacheSize = cacheSize;
        _storage = new Dictionary<long, Entry>(cacheSize);
    }

    public CacheItem? Get(long userId, DateTime now, TimeSpan ttl)
    {
        ValidateTtl(ttl);

        lock (_syncRoot)
        {
            if (!_storage.TryGetValue(userId, out var entry))
                return null;

            if (IsExpired(entry.Item, now, ttl))
            {
                Remove(userId);
                return null;
            }

            // Move the existing node to MRU in O(1).
            _lru.Remove(entry.Node);
            _lru.AddLast(entry.Node);
            return entry.Item;
        }
    }

    public void Add(UserProfile profile, DateTime now, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateTtl(ttl);

        lock (_syncRoot)
        {
            // An updated profile is a new entry with a new TTL and MRU position.
            Remove(profile.Id);

            if (_storage.Count >= _cacheSize)
            {
                // On overflow remove expired entries before evicting a valid LRU.
                var expiredIds = new List<long>();
                foreach (var (id, entry) in _storage)
                {
                    if (IsExpired(entry.Item, now, ttl))
                        expiredIds.Add(id);
                }

                foreach (var expiredId in expiredIds)
                    Remove(expiredId);

                if (_storage.Count >= _cacheSize)
                    Remove(_lru.First!.Value);
            }

            var node = _lru.AddLast(profile.Id);
            _storage.Add(profile.Id, new Entry(new CacheItem(profile, now), node));
        }
    }

    private static bool IsExpired(CacheItem item, DateTime now, TimeSpan ttl) =>
        now - item.SavedAt >= ttl;

    private static void ValidateTtl(TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl));
    }

    private void Remove(long userId)
    {
        if (_storage.Remove(userId, out var entry))
            _lru.Remove(entry.Node);
    }
}
