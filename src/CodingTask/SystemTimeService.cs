namespace CodingTask;

internal sealed class SystemTimeService : ISystemTimeService
{
    public DateTime UtcNow() => DateTime.UtcNow;
}