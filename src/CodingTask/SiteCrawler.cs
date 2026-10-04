using System.Collections.Concurrent;

namespace CodingTask;

/// <summary>
/// Crawler.
/// </summary>
public sealed class SiteCrawler : ISiteCrawler
{
    private readonly IPageSource _pageSource;
    private readonly int _maxConcurrency;
    private readonly SemaphoreSlim _semaphoreSlim;
    
    public SiteCrawler(
        IPageSource pageSource,
        int maxConcurrency)
    {
        _pageSource = pageSource;
        _maxConcurrency = maxConcurrency;
        _semaphoreSlim = new SemaphoreSlim(maxConcurrency);
    }
    
    /// <summary>
    /// Составить карту сайта.
    /// </summary>
    /// <param name="startPage">Стартовая страница</param>
    /// <returns>Карта сайта в виде словаря.</returns>
    public async Task<IReadOnlyDictionary<Uri, PageResult>> CrawlAsync(Uri startPage)
    {
        var result = new ConcurrentDictionary<Uri, PageResult>();
        var queue = new ConcurrentQueue<Uri>();
        
        queue.Enqueue(startPage);
        result.TryAdd(startPage, new PageResult([], null));

        await Parallel.ForAsync(0, _maxConcurrency, async (int index, CancellationToken token) => await ProcessQueueAsync(index, result, queue, startPage.Host));
        
        return result;
    }

    private async ValueTask ProcessQueueAsync(
        int workerId,
        ConcurrentDictionary<Uri, PageResult> result,
        ConcurrentQueue<Uri> queue,
        string host
        )
    {
        await Task.Delay(workerId * 10);

        while (queue.Count > 0)
        {
            if (!queue.TryDequeue(out var uri))
            {
                continue;
            }

            PageResult pageResult;
            try
            {
                await _semaphoreSlim.WaitAsync();
                var pageUris = await _pageSource.GetLinksAsync(uri);
                _semaphoreSlim.Release();

                var set = new HashSet<Uri>();
                var hostRelevant = new List<Uri>();
                
                foreach (var pageUri in pageUris.Where(u => u.Host == host))
                {
                    if (!result.ContainsKey(pageUri) && pageUri != uri)
                    {
                        queue.Enqueue(pageUri);
                        result.TryAdd(pageUri, new PageResult([], null));
                    }

                    if (set.Add(pageUri))
                    {
                        hostRelevant.Add(pageUri);
                    }
                }
                
                pageResult = new PageResult(hostRelevant, null);
            }
            catch (Exception e)
            {
                pageResult = new PageResult([], e);
            }

            result[uri] = pageResult;
        }
    }
}