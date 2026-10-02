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
        CacheStorage cache,
        TimeSpan cacheDuration)
    {
        _externalProfileSource = externalProfileSource;
        _systemTimeService = systemTimeService;
        
        _cacheDuration = cacheDuration;
        _cache = cache;
    }
    
    public async Task<UserProfile?> GetAsync(long userId, CancellationToken cancellationToken)
    {
        var now = _systemTimeService.UtcNow();
        var fromCache = _cache.Get(userId);

        if (fromCache is not null && now < fromCache.SavedAt + _cacheDuration)
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
}
