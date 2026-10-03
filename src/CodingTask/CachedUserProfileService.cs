using System.Collections.Concurrent;

namespace CodingTask;

/// <summary>Decorator over a profile source with in-memory LRU + absolute TTL.</summary>
public sealed class CachedUserProfileService : IUserProfileSource
{
    private readonly IUserProfileSource _externalProfileSource;
    private readonly ISystemTimeService _systemTimeService;
    private readonly CacheStorage _cache;
    private readonly TimeSpan _cacheDuration;
    private readonly ConcurrentDictionary<long, Lazy<Task<UserProfile?>>> _oneFlight;

    public CachedUserProfileService(
        IUserProfileSource externalProfileSource,
        ISystemTimeService systemTimeService,
        CacheStorage cache,
        TimeSpan cacheDuration)
    {
        _externalProfileSource = externalProfileSource ?? throw new ArgumentNullException(nameof(externalProfileSource));
        _systemTimeService = systemTimeService ?? throw new ArgumentNullException(nameof(systemTimeService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        if (cacheDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(cacheDuration));
        _cacheDuration = cacheDuration;

        _oneFlight = new();
    }

    public async Task<UserProfile?> GetAsync(long userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fromCache = _cache.Get(
            userId, _systemTimeService.UtcNow(), _cacheDuration);

        if (fromCache is not null)
            return fromCache.Profile;
        
        var lazyTask = _oneFlight.GetOrAdd(userId, new Lazy<Task<UserProfile?>>(async () => await _externalProfileSource.GetAsync(userId, CancellationToken.None)));
        
        var profile = await lazyTask.Value;
        cancellationToken.ThrowIfCancellationRequested();

        if (profile is null)
            return null;

        // Start TTL after the source has completed, not before awaiting it.
        _cache.Add(profile, _systemTimeService.UtcNow(), _cacheDuration);
        return profile;
    }
}
