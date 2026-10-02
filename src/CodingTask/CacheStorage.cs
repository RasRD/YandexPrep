namespace CodingTask;

/// <summary>
/// Кеш-хранилище. Имеет фиксированный размер, вытесняет объект, который дольше всех не трогали.
/// </summary>
public class CacheStorage
{
    private readonly int _cacheSize;
    private readonly Dictionary<long, CacheItem> _storage;
    
    // Конструкция для LRU.
    private Node? _head;
    private Node? _tail;
    private readonly Dictionary<long, Node> _removeCandidates;

    public CacheStorage(int cacheSize)
    {
        _cacheSize = cacheSize;
        _storage = new Dictionary<long, CacheItem>(_cacheSize);
        _removeCandidates = new Dictionary<long, Node>(_cacheSize);
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

        var node = new Node
        {
            Id = profile.Id,
            Next = null,
            Previous = _tail
        };
        _tail?.Next = node;
        _tail = node;
        _removeCandidates[profile.Id] = node;
        if(_head is null) _head = node;
    }

    public CacheItem? Get(long userId)
    {
        _storage.TryGetValue(userId, out var item);
        if (item is not null) MarkAsUsed(userId);
        
        return item;
    }

    private void MarkAsUsed(long userId)
    {
        var node = _removeCandidates[userId];

        node.Previous?.Next = node.Next;
        node.Next?.Previous = node.Previous;

        node.Next = null;
        node.Previous = _tail;
        _tail?.Next = node;
        _tail = node;
    }
    
    private void Remove()
    {
        if (_head is null) return;
        
        var toRemove = _head;
        
        _removeCandidates.Remove(toRemove.Id);
        _storage.Remove(toRemove.Id);
        
        _head = toRemove.Next;
        _head?.Previous = null;
    }
    
    private void Remove(long userId)
    {
        if (!_removeCandidates.TryGetValue(userId, out var node)) return;
        
        node.Previous?.Next = node.Next;
        node.Next?.Previous = node.Previous;

        _removeCandidates.Remove(node.Id);
        _storage.Remove(node.Id);
    }

    private sealed class Node
    {
        public long Id { get; set; }
        public Node? Next { get; set; }
        public Node? Previous { get; set; }
    }
}