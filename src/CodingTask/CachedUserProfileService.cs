namespace CodingTask;

public sealed class CachedUserProfileService : IUserProfileSource
{
    private readonly IUserProfileSource _externalProfileSource;
    private readonly ISystemTimeService _systemTimeService;

    private readonly CacheStorage _cache;
    private readonly TimeSpan _cacheDuration;

    public CachedUserProfileService(
        IUserProfileSource externalProfileSource,
        ISystemTimeService systemTimeService,
        TimeSpan cacheDuration,
        int cacheSize)
    {
        _externalProfileSource = externalProfileSource;
        _systemTimeService = systemTimeService;
        
        _cacheDuration = cacheDuration;
        _cache = new CacheStorage(cacheSize);
    }
    
    public async Task<UserProfile?> GetAsync(long userId, CancellationToken cancellationToken)
    {
        var now = _systemTimeService.UtcNow();
        var fromCache = _cache.Get(userId);
        
        // Нет инфы.
        if (fromCache is null)
        {
            var userProfile = await _externalProfileSource.GetAsync(userId, cancellationToken);
            if (userProfile is null)
            {
                return null;
            }

            _cache.Add(userProfile, now);
            return userProfile;
        }
        
        // Есть инфа и она свежая.
        if (now < fromCache.SavedAt + _cacheDuration)
        {
            return fromCache.Profile;
        }
        
        var externalSourceProfile = await _externalProfileSource.GetAsync(userId, cancellationToken);
        if (externalSourceProfile is null)
        {
            return null;
        }
        _cache.Add(externalSourceProfile, now);
        return externalSourceProfile;
    }

    private class CacheStorage
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
    
    private sealed class CacheItem
    {
        public CacheItem(UserProfile profile, DateTime savedAt)
        {
            Profile = profile;
            SavedAt = savedAt;
        }

        public UserProfile Profile { get; set; }
        
        public DateTime SavedAt { get; set; }
    }
}
