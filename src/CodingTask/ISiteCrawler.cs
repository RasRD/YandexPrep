namespace CodingTask;

public interface ISiteCrawler
{
    Task<IReadOnlyDictionary<Uri, PageResult>> CrawlAsync(Uri startPage);
}
