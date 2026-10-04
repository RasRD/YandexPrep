namespace CodingTask;

/// <summary>
/// Crawler.
/// </summary>
public sealed class SiteCrawler : ISiteCrawler
{
    private readonly IPageSource _pageSource;
    
    public SiteCrawler(IPageSource pageSource)
    {
        _pageSource = pageSource;
    }
    
    /// <summary>
    /// Составить карту сайта.
    /// </summary>
    /// <param name="startPage">Стартовая страница</param>
    /// <returns>Карта сайта в виде словаря.</returns>
    public async Task<IReadOnlyDictionary<Uri, PageResult>> CrawlAsync(Uri startPage)
    {
        var result = new Dictionary<Uri, PageResult>();
        var queued = new HashSet<Uri>();
        var queue = new Queue<Uri>();
        
        queue.Enqueue(startPage);
        queued.Add(startPage);

        while (queue.Count > 0)
        {
            var uri = queue.Peek();

            PageResult pageResult;
            try
            {
                var pageUris = await _pageSource.GetLinksAsync(startPage);
                
                var hostRelevant = pageUris.Where(u => u.Host == startPage.Host).ToArray();
                pageResult = new PageResult(hostRelevant, null);
                
                foreach (var pageUri in hostRelevant)
                {
                    if (queued.Add(pageUri))
                    {
                        queue.Enqueue(pageUri);
                    }
                }
            }
            catch (Exception e)
            {
                pageResult = new PageResult([], e);
            }

            result[uri] = pageResult;
            queue.Dequeue();
        }
        
        return result;
    }
}