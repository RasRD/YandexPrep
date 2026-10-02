namespace CodingTask.Tests;

public class CacheStorageTests
{
    [Fact]
    public async Task ShouldReturn_And_Cache_From_Source()
    {
        var now = DateTime.UtcNow;
        var ttl = TimeSpan.FromSeconds(30);
        var id = 5;
        
        var userData1 = new UserProfile(1, "TestName", "test@email.com");
        var u1Data = now - ttl;
        
        var userData2 = new UserProfile(id, "TestName", "test@email.com");
        var u2Data = now - 2 * ttl;
        
        var userData3 = new UserProfile(id, "TestName", "test@email.com");
        var u3Data = now;
        
        var cacheStorage = new CacheStorage(2);
        
        cacheStorage.Add(userData1, u1Data);
        cacheStorage.Add(userData2, u2Data);
        cacheStorage.Add(userData3, u3Data);

        var data = cacheStorage.Get(id);
        
        // Assert
        Assert.Equal(data?.Profile, userData3);
        Assert.Equal(data?.SavedAt, now);
        
    }
    
    [Fact]
    public async Task Should_Remove_As_LRU()
    {
        var now = DateTime.UtcNow;
        
        var userData1 = new UserProfile(1, "TestName", "test@email.com");
        var userData2 = new UserProfile(2, "TestName", "test@email.com");
        var userData3 = new UserProfile(3, "TestName", "test@email.com");
        
        var cacheStorage = new CacheStorage(2);
        
        cacheStorage.Add(userData1, now);
        cacheStorage.Add(userData2, now);

        var u1 = cacheStorage.Get(userData1.Id);
        Assert.Equal(u1?.Profile, userData1);
        
        cacheStorage.Add(userData3, now);

        var u2 = cacheStorage.Get(userData2.Id);
        var u3 = cacheStorage.Get(userData3.Id);
        Assert.Equal(u3?.Profile, userData3);
        Assert.Equal(null, u2?.Profile);
    }
}