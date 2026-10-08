namespace CodingTask;

public interface IPaymentHistoryQuery
{
    public Task<IReadOnlyCollection<Payment>> ExecuteAsync(long userId, CancellationToken cancellationToken);
}