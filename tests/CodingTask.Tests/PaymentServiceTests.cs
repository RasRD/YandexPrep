using Moq;

namespace CodingTask.Tests;

public class PaymentServiceTests
{
    private readonly Mock<IPaymentHistoryQuery> _mockPaymentHistoryQuery;
    private readonly Mock<ISystemTimeService>  _mockSystemTimeService;
    private readonly Mock<IListLimitsQuery> _listLimitsQuery;

    public PaymentServiceTests()
    {
        _mockPaymentHistoryQuery = new ();
        _mockSystemTimeService = new ();
        _listLimitsQuery = new ();
    }
    
    [Fact]
    public async Task PaymentService_Apply_OneTime_Limit()
    {
        var userId = 1;
        var now = DateTime.UtcNow;
        var limit = new Limit(20, 4);
        var p1 = new Payment(3, now);
        var p2 = new Payment(2, now);
        
        _mockSystemTimeService.Setup(s => s.NowUtc())
            .Returns(now);
        _listLimitsQuery.Setup(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()) )
            .ReturnsAsync(limit);
        _mockPaymentHistoryQuery.Setup(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([p1, p2]);
        
        var service = new PaymentService(_mockPaymentHistoryQuery.Object, _listLimitsQuery.Object, _mockSystemTimeService.Object);

        var result = await service.ValidateAsync(userId, 5, CancellationToken.None);
        
        Assert.Equal(ValidationResult.OnePaymentLimitReached, result);
        _mockPaymentHistoryQuery.Verify(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()), Times.Never);
        _mockSystemTimeService.Verify(s => s.NowUtc(), Times.Once);
        _listLimitsQuery.Verify(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task PaymentService_Apply_Daily_Limit()
    {
        var userId = 1;
        var now = DateTime.UtcNow;
        var limit = new Limit(10, 10);
        var p1 = new Payment(3, now);
        var p3 = new Payment(3, now);
        
        _mockSystemTimeService.Setup(s => s.NowUtc())
            .Returns(now);
        _listLimitsQuery.Setup(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()) )
            .ReturnsAsync(limit);
        _mockPaymentHistoryQuery.Setup(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([p1, p3]);
        
        var service = new PaymentService(_mockPaymentHistoryQuery.Object, _listLimitsQuery.Object, _mockSystemTimeService.Object);

        var result = await service.ValidateAsync(userId, 6, CancellationToken.None);
        
        Assert.Equal(ValidationResult.DailyLimitReached, result);
        _mockPaymentHistoryQuery.Verify(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockSystemTimeService.Verify(s => s.NowUtc(), Times.Once);
        _listLimitsQuery.Verify(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task PaymentService_Ignore_Other_Days_History()
    {
        var userId = 1;
        var now = DateTime.UtcNow;
        var limit = new Limit(10, 10);
        var p1 = new Payment(3, now);
        var p2 = new Payment(9, now.AddDays(-1));
        var p3 = new Payment(3, now);
        
        _mockSystemTimeService.Setup(s => s.NowUtc())
            .Returns(now);
        _listLimitsQuery.Setup(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()) )
            .ReturnsAsync(limit);
        _mockPaymentHistoryQuery.Setup(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([p1, p2, p3]);
        
        var service = new PaymentService(_mockPaymentHistoryQuery.Object, _listLimitsQuery.Object, _mockSystemTimeService.Object);

        var result = await service.ValidateAsync(userId, 4, CancellationToken.None);
        
        Assert.Equal(ValidationResult.Valid, result);
        _mockPaymentHistoryQuery.Verify(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockSystemTimeService.Verify(s => s.NowUtc(), Times.Once);
        _listLimitsQuery.Verify(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Theory]
    [InlineData(-1, 10 )]
    [InlineData(1, -10 )]
    [InlineData(0, 10 )]
    [InlineData(1, 0 )]
    public async Task PaymentService_Throws_On_Invalid_Params(long userId, decimal amount)
    {
        var now = DateTime.UtcNow;
        var limit = new Limit(10, 10);
        
        _mockSystemTimeService.Setup(s => s.NowUtc())
            .Returns(now);
        _listLimitsQuery.Setup(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()) )
            .ReturnsAsync(limit);
        _mockPaymentHistoryQuery.Setup(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        
        var service = new PaymentService(_mockPaymentHistoryQuery.Object, _listLimitsQuery.Object, _mockSystemTimeService.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await service.ValidateAsync(userId, amount, CancellationToken.None));
        
        _mockPaymentHistoryQuery.Verify(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()), Times.Never);
        _mockSystemTimeService.Verify(s => s.NowUtc(), Times.Never);
        _listLimitsQuery.Verify(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()), Times.Never);
    }

    private static readonly DateTime FixedNow = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private PaymentService CreateService(Limit limit, params Payment[] history)
    {
        _mockSystemTimeService.Setup(s => s.NowUtc()).Returns(FixedNow);
        _listLimitsQuery.Setup(s => s.ExecuteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(limit);
        _mockPaymentHistoryQuery.Setup(s => s.ExecuteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(history);

        return new PaymentService(_mockPaymentHistoryQuery.Object, _listLimitsQuery.Object, _mockSystemTimeService.Object);
    }

    [Theory]
    [InlineData("350", ValidationResult.Valid)]
    [InlineData("350.01", ValidationResult.DailyLimitReached)]
    [InlineData("501", ValidationResult.OnePaymentLimitReached)]
    public async Task PaymentService_Readme_Example(string amount, ValidationResult expected)
    {
        var service = CreateService(
            new Limit(1000, 500),
            new Payment(650, FixedNow.AddHours(-1)),
            new Payment(900, FixedNow.AddDays(-1)));

        var result = await service.ValidateAsync(1, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task PaymentService_Allows_Amounts_Equal_To_Limits()
    {
        var service = CreateService(new Limit(1000, 500), new Payment(500, FixedNow.AddHours(-1)));

        var result = await service.ValidateAsync(1, 500, CancellationToken.None);

        Assert.Equal(ValidationResult.Valid, result);
    }

    [Fact]
    public async Task PaymentService_Prefers_OneTime_When_Both_Limits_Exceeded()
    {
        var service = CreateService(new Limit(1000, 500), new Payment(650, FixedNow.AddHours(-1)));

        var result = await service.ValidateAsync(1, 501, CancellationToken.None);

        Assert.Equal(ValidationResult.OnePaymentLimitReached, result);
    }

    [Fact]
    public async Task PaymentService_Counts_Only_Current_Utc_Day()
    {
        var dayStart = FixedNow.Date;
        var service = CreateService(
            new Limit(100, 100),
            new Payment(50, dayStart),
            new Payment(1000, dayStart.AddTicks(-1)),
            new Payment(1000, dayStart.AddDays(1)));

        Assert.Equal(ValidationResult.Valid, await service.ValidateAsync(1, 50, CancellationToken.None));
        Assert.Equal(ValidationResult.DailyLimitReached, await service.ValidateAsync(1, 50.01m, CancellationToken.None));
    }

    [Fact]
    public async Task PaymentService_Throws_On_Cancelled_Token_Without_Calling_Dependencies()
    {
        var service = CreateService(new Limit(10, 10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ValidateAsync(1, 5, new CancellationToken(canceled: true)));

        _mockSystemTimeService.Verify(s => s.NowUtc(), Times.Never);
        _listLimitsQuery.Verify(s => s.ExecuteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPaymentHistoryQuery.Verify(s => s.ExecuteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PaymentService_Passes_Caller_Token_To_Dependencies()
    {
        using var cts = new CancellationTokenSource();
        var service = CreateService(new Limit(10, 10));

        await service.ValidateAsync(1, 5, cts.Token);

        _listLimitsQuery.Verify(s => s.ExecuteAsync(1, cts.Token), Times.Once);
        _mockPaymentHistoryQuery.Verify(s => s.ExecuteAsync(1, cts.Token), Times.Once);
    }

    [Fact]
    public async Task PaymentService_Propagates_Dependency_Cancellation()
    {
        var service = CreateService(new Limit(10, 10));
        _mockPaymentHistoryQuery.Setup(s => s.ExecuteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ValidateAsync(1, 5, CancellationToken.None));
    }

    [Fact]
    public async Task PaymentService_Propagates_Dependency_Exception()
    {
        var service = CreateService(new Limit(10, 10));
        _listLimitsQuery.Setup(s => s.ExecuteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateAsync(1, 5, CancellationToken.None));
    }

    [Fact]
    public async Task PaymentService_Reads_Time_Once_Before_Waiting_For_Dependencies()
    {
        var limitsSource = new TaskCompletionSource<Limit>();
        var dayStart = FixedNow.Date;
        _mockSystemTimeService.SetupSequence(s => s.NowUtc())
            .Returns(dayStart.AddTicks(-1))
            .Returns(dayStart);
        _listLimitsQuery.Setup(s => s.ExecuteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(limitsSource.Task);
        _mockPaymentHistoryQuery.Setup(s => s.ExecuteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Payment(100, dayStart.AddHours(-1))]);
        var service = new PaymentService(_mockPaymentHistoryQuery.Object, _listLimitsQuery.Object, _mockSystemTimeService.Object);

        var task = service.ValidateAsync(1, 1, CancellationToken.None);
        _mockSystemTimeService.Verify(s => s.NowUtc(), Times.Once);
        limitsSource.SetResult(new Limit(100, 100));

        Assert.Equal(ValidationResult.DailyLimitReached, await task);
        _mockSystemTimeService.Verify(s => s.NowUtc(), Times.Once);
    }
}
