namespace CodingTask;

public interface IPageSource
{
    Task<IReadOnlyList<Uri>> GetLinksAsync(Uri page);
}
