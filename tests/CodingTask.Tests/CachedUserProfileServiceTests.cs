using Moq;

namespace CodingTask.Tests;

public class CachedUserProfileServiceTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly DateTime Start = new(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly UserProfile User = new(5, "TestName", "test@email.com");

    [Fact]
    public async Task GetAsync_CachesSuccessfulResult()
    {
        var now = Start;
        var source = new Mock<IUserProfileSource>();
        var clock = new Mock<ISystemTimeService>();
        clock.Setup(x => x.UtcNow()).Returns(() => now);
        source.Setup(x => x.GetAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User);
        var service = new CachedUserProfileService(source.Object, clock.Object, new CacheStorage(2), Ttl);

        Assert.Equal(User, await service.GetAsync(5, CancellationToken.None));
        now += TimeSpan.FromSeconds(10);
        Assert.Equal(User, await service.GetAsync(5, CancellationToken.None));

        source.Verify(x => x.GetAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_AfterTtl_FetchesAgain()
    {
        var now = Start;
        var source = new Mock<IUserProfileSource>();
        var clock = new Mock<ISystemTimeService>();
        clock.Setup(x => x.UtcNow()).Returns(() => now);
        var updated = User with { Name = "Updated" };
        source.SetupSequence(x => x.GetAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User)
            .ReturnsAsync(updated);
        var service = new CachedUserProfileService(source.Object, clock.Object, new CacheStorage(2), Ttl);

        Assert.Equal(User, await service.GetAsync(5, CancellationToken.None));
        now += Ttl;
        Assert.Equal(updated, await service.GetAsync(5, CancellationToken.None));
        source.Verify(x => x.GetAsync(5, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_UsesCompletionTimeForTtl()
    {
        var now = Start;
        var source = new Mock<IUserProfileSource>();
        var clock = new Mock<ISystemTimeService>();
        clock.Setup(x => x.UtcNow()).Returns(() => now);
        source.Setup(x => x.GetAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                now += TimeSpan.FromSeconds(20); // Simulate a slow fetch.
                return User;
            });
        var service = new CachedUserProfileService(source.Object, clock.Object, new CacheStorage(2), Ttl);

        await service.GetAsync(5, CancellationToken.None);
        now = Start + TimeSpan.FromSeconds(31);
        Assert.Equal(User, await service.GetAsync(5, CancellationToken.None));
        source.Verify(x => x.GetAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_DoesNotCacheNull()
    {
        var source = new Mock<IUserProfileSource>();
        var clock = new Mock<ISystemTimeService>();
        clock.Setup(x => x.UtcNow()).Returns(Start);
        source.Setup(x => x.GetAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfile?)null);
        var service = new CachedUserProfileService(source.Object, clock.Object, new CacheStorage(2), Ttl);

        Assert.Null(await service.GetAsync(5, CancellationToken.None));
        Assert.Null(await service.GetAsync(5, CancellationToken.None));
        source.Verify(x => x.GetAsync(5, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_PropagatesSourceFailureWithoutCaching()
    {
        var source = new Mock<IUserProfileSource>();
        var clock = new Mock<ISystemTimeService>();
        clock.Setup(x => x.UtcNow()).Returns(Start);
        source.Setup(x => x.GetAsync(5, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Source unavailable"));
        var service = new CachedUserProfileService(source.Object, clock.Object, new CacheStorage(2), Ttl);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAsync(5, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAsync(5, CancellationToken.None));
        source.Verify(x => x.GetAsync(5, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetAsync_ConcurrentMisses_FetchOnce()
    {
        const int callers = 100;
        var source = new Mock<IUserProfileSource>();
        var clock = new Mock<ISystemTimeService>();
        clock.Setup(x => x.UtcNow()).Returns(Start);
        // Hold the fetch open so every caller misses the cache while it is in flight.
        var fetch = new TaskCompletionSource<UserProfile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Setup(x => x.GetAsync(5, It.IsAny<CancellationToken>()))
            .Returns(fetch.Task);
        var service = new CachedUserProfileService(source.Object, clock.Object, new CacheStorage(2), Ttl);

        var started = 0;
        var requests = Enumerable.Range(0, callers)
            .Select(_ => Task.Run(() =>
            {
                var request = service.GetAsync(5, CancellationToken.None);
                Interlocked.Increment(ref started);
                return request;
            }))
            .ToArray();

        // GetAsync has returned for every caller, so all of them are awaiting the fetch.
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref started) == callers, TimeSpan.FromSeconds(5)));
        fetch.SetResult(User);

        var results = await Task.WhenAll(requests);
        Assert.All(results, profile => Assert.Equal(User, profile));
        source.Verify(x => x.GetAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_DoesNotForwardCallerTokenToSharedFetch()
    {
        using var cts = new CancellationTokenSource();
        var source = new Mock<IUserProfileSource>();
        var clock = new Mock<ISystemTimeService>();
        clock.Setup(x => x.UtcNow()).Returns(Start);
        source.Setup(x => x.GetAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User);
        var service = new CachedUserProfileService(source.Object, clock.Object, new CacheStorage(2), Ttl);

        await service.GetAsync(5, cts.Token);
        // The fetch is shared, so one caller's token must not cancel it for the others.
        source.Verify(x => x.GetAsync(5, It.Is<CancellationToken>(t => !t.CanBeCanceled)), Times.Once);
    }

    [Fact]
    public async Task GetAsync_CancelledWaiter_ThrowsWhileOthersGetResult()
    {
        using var cts = new CancellationTokenSource();
        var source = new Mock<IUserProfileSource>();
        var clock = new Mock<ISystemTimeService>();
        clock.Setup(x => x.UtcNow()).Returns(Start);
        var fetch = new TaskCompletionSource<UserProfile?>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Setup(x => x.GetAsync(5, It.IsAny<CancellationToken>()))
            .Returns(fetch.Task);
        var service = new CachedUserProfileService(source.Object, clock.Object, new CacheStorage(2), Ttl);

        var cancelled = service.GetAsync(5, cts.Token);
        var other = service.GetAsync(5, CancellationToken.None);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(other.IsCompleted);

        fetch.SetResult(User);
        Assert.Equal(User, await other);
        Assert.Equal(User, await service.GetAsync(5, CancellationToken.None));
        source.Verify(x => x.GetAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenCancelled_ThrowsAndDoesNotFetch()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var source = new Mock<IUserProfileSource>();
        var clock = new Mock<ISystemTimeService>();
        var service = new CachedUserProfileService(source.Object, clock.Object, new CacheStorage(2), Ttl);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(5, cts.Token));
        source.Verify(x => x.GetAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
