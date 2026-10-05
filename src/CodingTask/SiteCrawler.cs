using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CodingTask;

/// <summary>
/// Crawler.
/// </summary>
public sealed class SiteCrawler : ISiteCrawler
{
    private readonly IPageSource _pageSource;
    private readonly int _maxConcurrency;

    public SiteCrawler(
        IPageSource pageSource,
        int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        _pageSource = pageSource;
        _maxConcurrency = maxConcurrency;
    }
    
    /// <summary>
    /// Составить карту сайта.
    /// </summary>
    /// <param name="startPage">Стартовая страница</param>
    /// <returns>Карта сайта в виде словаря.</returns>
    public async Task<IReadOnlyDictionary<Uri, PageResult>> CrawlAsync(Uri startPage)
    {
        var result = new ConcurrentDictionary<Uri, PageResult>();
        var queue = Channel.CreateUnbounded<Uri>();
        
        result.TryAdd(startPage, new PageResult([], null));
        await queue.Writer.WriteAsync(startPage);
        var counter = new StrongBox<int>(1);
        
        var tasks = new List<Task>();
        for(var i = 0; i < _maxConcurrency; i++)
        {
            tasks.Add(DoWork(result, queue, counter, startPage.Host));
        }
        await Task.WhenAll(tasks);
        
        return result;
    }

    private async Task DoWork(
        ConcurrentDictionary<Uri, PageResult> result,
        Channel<Uri> queue,
        StrongBox<int> counter,
        string host)
    {
        await foreach (var uri in queue.Reader.ReadAllAsync())
        {
            PageResult pageResult;
            try
            {
                var pageUris = await _pageSource.GetLinksAsync(uri);
                var set = new HashSet<Uri>();
                var hostRelevant = new List<Uri>();
                
                foreach (var pageUri in pageUris.Where(u => u.Host == host))
                {
                    if (set.Add(pageUri))
                    {
                        hostRelevant.Add(pageUri);
                    }
                    
                    if (result.TryAdd(pageUri, new PageResult([], null)))
                    {
                        Interlocked.Increment(ref counter.Value);
                        await queue.Writer.WriteAsync(pageUri);
                    }
                }
                
                pageResult = new PageResult(hostRelevant, null);
            }
            catch (Exception e)
            {
                pageResult = new PageResult([], e);
            }     
            
            result[uri] = pageResult;
            var val = Interlocked.Decrement(ref counter.Value);
            if(val == 0) queue.Writer.Complete();
        }
    }
}