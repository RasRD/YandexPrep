namespace CodingTask;

public sealed class OrderService
{
    private readonly Dictionary<long, SkuData> _store;
    
    public OrderService(Dictionary<long, SkuData> store)
    {
        _store = store;
    }

    public decimal MakeOrder(ClientData client, IReadOnlyCollection<(long Id, int Count)> basket)
    {
        var order = new Dictionary<long, int>();
        foreach (var item in basket)
        {
            if (!order.ContainsKey(item.Id))
            {
                order.Add(item.Id, item.Count);
            }
            else
            {
                order[item.Id] += item.Count;
            }
        }

        decimal total = 0;
        foreach (var item in order)
        {
            if (!_store.ContainsKey(item.Key) || item.Value > _store[item.Key].Count)
            {
                throw new InvalidOperationException($"Order {item.Key} does not exist");
            }

            total += item.Value * _store[item.Key].Price;
            _store[item.Key].Count -= item.Value;
        }


        total *= (decimal)(1 - client.Discount * 0.01) ;
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
}