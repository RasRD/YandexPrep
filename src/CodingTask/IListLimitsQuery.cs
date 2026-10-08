namespace CodingTask;

public record Limit(decimal Daily, decimal OneTime);

public interface IListLimitsQuery
{
    public Task<Limit> ExecuteAsync(long userId, CancellationToken cancellationToken);
}