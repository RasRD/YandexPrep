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
        _mockPaymentHistoryQuery.Verify(s => s.ExecuteAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
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
}