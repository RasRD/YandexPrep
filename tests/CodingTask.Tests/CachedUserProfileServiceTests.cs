using Moq;

namespace CodingTask.Tests;

public class CachedUserProfileServiceTests
{
    private readonly Mock<IUserProfileSource> _usersProfileSourceMock = new ();
    private readonly Mock<ISystemTimeService> _systemTimeServiceMock = new ();

    public CachedUserProfileServiceTests()
    {
    }

    [Fact]
    public async Task ShouldReturn_And_Cache_From_Source()
    {
        var now = DateTime.UtcNow;
        var ttl = TimeSpan.FromSeconds(30);
        var userData = new UserProfile(5, "TestName", "test@email.com");
        var cacheStorage = new CacheStorage(2);
        
        var service = new CachedUserProfileService(
            _usersProfileSourceMock.Object,
            _systemTimeServiceMock.Object,
            cacheStorage,
            ttl);
        
        _usersProfileSourceMock.Setup(m => m.GetAsync(userData.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userData);

        _systemTimeServiceMock.Setup(m => m.UtcNow()).Returns(now);
        
        // Act
        var result = await service.GetAsync(userData.Id, CancellationToken.None);
        Assert.Equal(userData, result);
        
        _usersProfileSourceMock
            .Verify(m => m.GetAsync(userData.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task Should_Remove_By_TTL()
    {
        var now = DateTime.UtcNow;
        var ttl = TimeSpan.FromSeconds(30);
        
        var cachedUserData = new UserProfile(5, "TestName", "test@email.com");
        var externalServcieUserData = new UserProfile(5, "ExtTestName", "exttest@email.com");
        var cacheStorage = new CacheStorage(2);
        cacheStorage.Add(cachedUserData, now - ttl);
        
        var service = new CachedUserProfileService(
            _usersProfileSourceMock.Object,
            _systemTimeServiceMock.Object,
            cacheStorage,
            ttl);
        
        _usersProfileSourceMock.Setup(m => m.GetAsync(cachedUserData.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalServcieUserData);

        _systemTimeServiceMock.Setup(m => m.UtcNow()).Returns(now);
        
        // Act
        var result = await service.GetAsync(cachedUserData.Id, CancellationToken.None);
        
        _usersProfileSourceMock
            .Verify(m => m.GetAsync(cachedUserData.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(externalServcieUserData, result);
    }
}