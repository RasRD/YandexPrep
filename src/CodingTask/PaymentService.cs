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
        if (userId <= 0L) throw new ArgumentOutOfRangeException(nameof(userId));

        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount));

        cancellationToken.ThrowIfCancellationRequested();

        var now = _systemTimeService.NowUtc();
        var dayStart = now.Date;
        var dayEnd = dayStart.AddDays(1);

        var limits = await _listLimitsQuery.ExecuteAsync(userId, cancellationToken);

        if (limits.OneTime < amount)
        {
            return ValidationResult.OnePaymentLimitReached;
        }

        var history = await _historyQuery.ExecuteAsync(userId, cancellationToken);

        decimal paid = 0;

        foreach (var item in history)
        {
            if (item.PaymentDate >= dayStart && item.PaymentDate < dayEnd)
            {
                paid += item.Amount;
            }
        }

        if (limits.Daily < paid + amount)
        {
            return ValidationResult.DailyLimitReached;
        }

        return ValidationResult.Valid;
    }
}