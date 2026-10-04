using Moq;

namespace CodingTask.Tests;

public class SiteCrawlerTests
{
    private readonly Mock<IPageSource>  _pageSource;

    public SiteCrawlerTests()
    {
        _pageSource = new Mock<IPageSource>();
    }

    [Fact]
    public async Task SiteCrawler_Should_Return_Uniq()
    {
        var start = new Uri("https://www.test.com/start");
        var u1 = new Uri("https://www.test.com/u1");
        var u2 = new Uri("https://www.test.com/u2");

        _pageSource
            .Setup(p => p.GetLinksAsync(start))
            .ReturnsAsync(new List<Uri> { u1, u2 });
        
        _pageSource
            .Setup(p => p.GetLinksAsync(u1))
            .ReturnsAsync(new List<Uri> { u1 });
        
        _pageSource
            .Setup(p => p.GetLinksAsync(u2))
            .ReturnsAsync([]);
        
        var service = new SiteCrawler(_pageSource.Object);
        
        var testResult = await service.CrawlAsync(start);
        
        Assert.Equal(3, testResult.Count);
        Assert.Equal([], testResult[u2].Links);
        Assert.Equal([], testResult[u1].Links);
    }
    
    [Fact]
    public async Task SiteCrawler_Should_Save_Error()
    {
        var start = new Uri("https://www.test.com/start");
        var u1 = new Uri("https://www.test.com/u1");
        var u2 = new Uri("https://www.test.com/u2");

        _pageSource
            .Setup(p => p.GetLinksAsync(start))
            .ReturnsAsync(new List<Uri> { u1, u2 });
        
        _pageSource
            .Setup(p => p.GetLinksAsync(u1))
            .ReturnsAsync(new List<Uri> { u1 });

        var exc = new ArgumentException("E_TEST_ERROR");
        _pageSource
            .Setup(p => p.GetLinksAsync(u2))
            .ThrowsAsync(exc);
        
        var service = new SiteCrawler(_pageSource.Object);
        
        var testResult = await service.CrawlAsync(start);
        
        Assert.Equal(3, testResult.Count);
        Assert.Equal([], testResult[u2].Links);
        Assert.Equal(exc, testResult[u2].Error);
        Assert.Equal([], testResult[u1].Links);
    }
}