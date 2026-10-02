namespace CodingTask;

/// <summary>Decorator over a profile source with in-memory LRU + absolute TTL.</summary>
public sealed class CachedUserProfileService : IUserProfileSource
{
    private readonly IUserProfileSource _externalProfileSource;
    private readonly ISystemTimeService _systemTimeService;
    private readonly CacheStorage _cache;
    private readonly TimeSpan _cacheDuration;

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
    }

    public async Task<UserProfile?> GetAsync(long userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fromCache = _cache.Get(
            userId, _systemTimeService.UtcNow(), _cacheDuration);

        if (fromCache is not null)
            return fromCache.Profile;

        var profile = await _externalProfileSource.GetAsync(userId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (profile is null)
            return null;

        // Start TTL after the source has completed, not before awaiting it.
        _cache.Add(profile, _systemTimeService.UtcNow(), _cacheDuration);
        return profile;
    }
}
