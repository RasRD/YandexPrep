namespace CodingTask;

/// <summary>
/// Кеш-хранилище. Имеет фиксированный размер, вытесняет объект, который дольше всех не трогали.
/// </summary>
public class CacheStorage
{
    private readonly int _cacheSize;
    private readonly Dictionary<long, CacheItem> _storage;
    private readonly LinkedList<long> _lru;

    public CacheStorage(int cacheSize)
    {
        _cacheSize = cacheSize;
        _storage = new Dictionary<long, CacheItem>(_cacheSize);
        _lru = new();
    }

    public void Add(UserProfile profile, DateTime now)
    {
        if (_storage.ContainsKey(profile.Id))
        {
            Remove(profile.Id);
        } 
        else if (_storage.Count >= _cacheSize)
        {
            Remove();
        }
        
        var item = new CacheItem(profile, now);
        _storage[profile.Id] = item;
    }

    public CacheItem? Get(long userId)
    {
        _storage.TryGetValue(userId, out var item);
        if (item is not null) MarkAsUsed(userId);
        
        return item;
    }

    private void MarkAsUsed(long userId)
    {
        _lru.Remove(userId);
        _lru.AddLast(userId);
    }
    
    private void Remove()
    {
        var id = _lru.First!.Value;

        _lru.RemoveFirst();
        _storage.Remove(id);
    }
    
    private void Remove(long userId)
    {
        _lru.Remove(userId);
        _storage.Remove(userId);
    }
}