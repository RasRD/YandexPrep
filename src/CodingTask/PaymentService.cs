namespace CodingTask;

public enum ValidationResult
{
    Valid = 0,
    DailyLimitReached = 1,
    OnePaymentLimitReached = 2
}

public sealed class PaymentService
{
    private readonly IPaymentHistoryQuery  _historyQuery;
    private readonly IListLimitsQuery  _listLimitsQuery;
    private readonly ISystemTimeService _systemTimeService;

    public PaymentService(
        IPaymentHistoryQuery paymentHistoryQuery,
        IListLimitsQuery  listLimitsQuery,
        ISystemTimeService systemTimeService)
    {
        _historyQuery = paymentHistoryQuery;
        _listLimitsQuery = listLimitsQuery;
        _systemTimeService = systemTimeService;
    }
    
    public async Task<ValidationResult> ValidateAsync(
        long userId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if(userId < 0L) throw new ArgumentOutOfRangeException(nameof(userId));
        
        if(amount < 0L) throw new ArgumentOutOfRangeException(nameof(amount));

        var story = await _historyQuery.ExecuteAsync(userId, cancellationToken);
        
        var now = _systemTimeService.NowUtc();
        decimal paied = 0;

        foreach (var item in story)
        {
            var paymentDate = item.PaymentDate;
            
            if(paymentDate.Kind != DateTimeKind.Utc) throw new ArgumentOutOfRangeException(nameof(paymentDate));

            if (paymentDate.Day.Equals(now.Day) && paymentDate.Month.Equals(now.Month) &&
                paymentDate.Year.Equals(now.Year))
            {
                if(amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
                paied += item.Amount;
            }
        }
        
        var limits = await _listLimitsQuery.ExecuteAsync(userId, cancellationToken);

        if (limits.Daily < paied + amount)
        {
            return ValidationResult.DailyLimitReached;
        }
        
        if (limits.OneTime < amount)
        {
            return ValidationResult.OnePaymentLimitReached;
        }
        
        return ValidationResult.Valid;
    }
}