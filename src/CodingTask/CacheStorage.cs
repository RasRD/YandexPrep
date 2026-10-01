namespace CodingTask;

public class CacheStorage
{
    private readonly int _cacheSize;
    private readonly Dictionary<long, CacheItem> _storage;
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
        _storage.Remove(profile.Id);

        if (_storage.Count >= _cacheSize)
        {
            var toRemove = _head;

            if (toRemove != null)
            {
                _head = toRemove.Next;
                _removeCandidates.Remove(toRemove.Id);
                _storage.Remove(toRemove.Id);
            }
        }
        
        var cacheItem = new CacheItem(profile, now);
        var removeCand = new Node()
        {
            Id = profile.Id,
            Next = null,
            Previous = _tail
        };

        _storage[profile.Id] = cacheItem;
        _tail = removeCand;
        _removeCandidates[removeCand.Id] = removeCand;
    }

    public CacheItem? Get(long userId)
    {
        if (!_storage.TryGetValue(userId, out var item))
        {
            return null;
        }

        if (!_removeCandidates.TryGetValue(userId, out var cand))
        {
            cand = new Node
            {
                Id = userId,
                Next = null,
                Previous = _tail
            };
            _tail = cand;
            _removeCandidates[userId] = cand;
        }
        else
        {
            var priv = cand.Previous;
            var next = cand.Next;

            priv?.Next = next;
            next?.Previous = priv;
            
            _tail = cand;
        }
        
        return item;
    }

    private class Node
    {
        public long Id { get; set; }
        public Node? Next { get; set; }
        public Node? Previous { get; set; }
    }
    
}