using System.Collections.Concurrent;

namespace CodingTask;

public sealed class PaymentService2
{
    private readonly ConcurrentDictionary<long, Mutex> _locks = new();
    private readonly ConcurrentDictionary<long, List<Payment>> _history;
    private readonly IReadOnlyDictionary<long, Limit> _limits;
    private readonly ISystemTimeService _systemTimeService;

    public PaymentService2(
        ConcurrentDictionary<long, List<Payment>> history,
        IReadOnlyDictionary<long, Limit>  limits,
        ISystemTimeService systemTimeService)
    {
        _history = history;
        _limits = limits;
        _systemTimeService = systemTimeService;
    }
    
    public async Task<IDictionary<long, List<Payment>>> ValidateAsync(
        long userId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(userId, 0L);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, 0m);

        cancellationToken.ThrowIfCancellationRequested();

        var now = _systemTimeService.NowUtc();
        var dayStart = now.Date;
        var dayEnd = dayStart.AddDays(1);

        if (!_limits.TryGetValue(userId, out var limits))
        {
            throw new Exception($"No limit found for user id {userId}");
        }

        if (limits.OneTime < amount)
        {
            return _history;
        }

        // LOCK
        var mutex = _locks.GetOrAdd(userId, _ => new Mutex(true, $"PaymentService2-{userId}"));
        try
        {
            mutex.WaitOne();
            
            // GET
            var history = _history.GetOrAdd(userId, []);

            // Calculate
            decimal paid = 0;
            foreach (var item in history)
            {
                if (item.PaymentDate >= dayStart && item.PaymentDate < dayEnd)
                {
                    paid += item.Amount;
                }
            }

            // CHECK
            if (limits.Daily < paid + amount)
            {
                return _history;
            }

            // ADD
            cancellationToken.ThrowIfCancellationRequested();
            history.Add(new Payment(amount, now));

            return _history;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}