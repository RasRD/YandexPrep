namespace CodingTask.Tests;

public class OrderServiceTests
{
    [Fact]
    public void OrderService_Should_Apply_Discount()
    {
        var store = new Dictionary<long, SkuData>
        {
            { 1, new SkuData() { Id = 1, Count = 10, Price = 7 } },
            { 2, new SkuData() { Id = 2, Count = 10, Price = 13 } }
        };
        var person = new ClientData("Test", 6);

        var service = new OrderService(store);
        
        var totalPrice = service.MakeOrder(person, [(1, 3), (2, 2)]);
        
        Assert.Equal((decimal)44.18, totalPrice);
    }
    
    [Fact]
    public async Task OrderService_Should_Process_ParallelOrders()
    {
        var store = new Dictionary<long, SkuData>
        {
            { 1, new SkuData() { Id = 1, Count = 2, Price = 10 } }
        };
        var person = new ClientData("Test", 10);

        var service = new OrderService(store);

        using var barrier = new Barrier(3);
        var orders = Enumerable.Range(0, 3)
            .Select(_ => Task.Run(() =>
            {
                barrier.SignalAndWait();
                return service.MakeOrder(person, [(1, 1)]);
            }))
            .ToArray();

        try
        {
            await Task.WhenAll(orders);
        }
        catch (InvalidOperationException)
        {
            // Ожидаем один отказ — проверяем каждую задачу ниже
        }

        Assert.Equal(2, orders.Count(t => t.IsCompletedSuccessfully));
        var failed = Assert.Single(orders, t => t.IsFaulted);
        Assert.IsType<InvalidOperationException>(failed.Exception!.InnerException);
        Assert.All(orders.Where(t => t.IsCompletedSuccessfully), t => Assert.Equal(9m, t.Result));
        Assert.Equal(0, store[1].Count);
    }
    
    [Fact]
    public void OrderService_Should_Decrease_Sku_Number()
    {
        var store = new Dictionary<long, SkuData>
        {
            { 1, new SkuData() { Id = 1, Count = 10, Price = 7 } },
            { 2, new SkuData() { Id = 2, Count = 10, Price = 13 } }
        };
        var person = new ClientData("Test", 6);

        var service = new OrderService(store);
        
        service.MakeOrder(person, [(1, 3), (2, 2)]);
        
        Assert.Equal(7, store[1].Count);
        Assert.Equal(8, store[2].Count);
    }
    
    [Fact]
    public void OrderService_Should_Deal_With_Duplicates()
    {
        var store = new Dictionary<long, SkuData>
        {
            { 1, new SkuData() { Id = 1, Count = 10, Price = 7 } },
            { 2, new SkuData() { Id = 2, Count = 10, Price = 13 } }
        };
        var person = new ClientData("Test", 6);

        var service = new OrderService(store);
        
        var totalPrice = service.MakeOrder(person, [(1, 2), (2, 2), (1, 1)]);
        
        Assert.Equal((decimal)44.18, totalPrice);
        Assert.Equal(7, store[1].Count);
        Assert.Equal(8, store[2].Count);
    }
}