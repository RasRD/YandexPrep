namespace CodingTask.Tests;

public class CacheStorageTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly DateTime Now = new(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);

    private static UserProfile Profile(long id) => new(id, $"Name{id}", $"user{id}@test.com");

    [Fact]
    public void Get_ReturnsCachedProfile()
    {
        var cache = new CacheStorage(2);
        var profile = Profile(1);
        cache.Add(profile, Now, Ttl);

        var result = cache.Get(profile.Id, Now, Ttl);

        Assert.Equal(profile, result?.Profile);
        Assert.Equal(Now, result?.SavedAt);
    }

    [Fact]
    public void Get_WhenTtlExpires_ReturnsNullAndRemovesEntry()
    {
        var cache = new CacheStorage(2);
        cache.Add(Profile(1), Now, Ttl);

        Assert.Null(cache.Get(1, Now + Ttl, Ttl));
        Assert.Null(cache.Get(1, Now + Ttl, Ttl));
    }

    [Fact]
    public void Add_WhenFull_EvictsLeastRecentlyUsed()
    {
        var cache = new CacheStorage(2);
        cache.Add(Profile(1), Now, Ttl);
        cache.Add(Profile(2), Now, Ttl);
        Assert.NotNull(cache.Get(1, Now, Ttl)); // 1 becomes MRU.

        cache.Add(Profile(3), Now, Ttl);

        Assert.Null(cache.Get(2, Now, Ttl));
        Assert.NotNull(cache.Get(1, Now, Ttl));
        Assert.NotNull(cache.Get(3, Now, Ttl));
    }

    [Fact]
    public void Add_WhenFull_EvictsExpiredBeforeValidLru()
    {
        var cache = new CacheStorage(2);
        cache.Add(Profile(2), Now, Ttl); // valid and LRU
        cache.Add(Profile(1), Now - Ttl, Ttl); // expired, but MRU
        // The expired MRU must go before the valid LRU.
        cache.Add(Profile(3), Now, Ttl);

        Assert.Null(cache.Get(1, Now, Ttl));
        Assert.NotNull(cache.Get(2, Now, Ttl));
        Assert.NotNull(cache.Get(3, Now, Ttl));
    }

    [Fact]
    public void Add_ReplacesExistingValueAndResetsTtl()
    {
        var cache = new CacheStorage(1);
        cache.Add(new UserProfile(1, "Old", "old@example.com"), Now - Ttl, Ttl);
        cache.Add(new UserProfile(1, "New", "new@example.com"), Now, Ttl);

        var result = cache.Get(1, Now, Ttl);

        Assert.Equal("New", result?.Profile.Name);
        Assert.Equal(Now, result?.SavedAt);
    }

    [Fact]
    public void Constructor_RejectsZeroCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CacheStorage(0));
    }
}
