using System.Collections.Concurrent;

namespace CodingTask;

public sealed class OrderService
{
    private readonly Dictionary<long, SkuData> _store;
    private readonly SkuLocks  _locks;
    
    public OrderService(Dictionary<long, SkuData> store)
    {
        _store = store;
        _locks = new SkuLocks();
    }

    public SkuData GetSku(long id)
    {
        using var l = _locks.Lock([id]);
        return _store[id];
    }

    public decimal MakeOrder(ClientData client, IReadOnlyCollection<(long Id, int Count)> basket)
    {
        if (client is null || client.Discount is > 100 or < 0)
        {
            throw new InvalidOperationException();
        }

        if (basket.Count == 0 || basket.Any(b => b.Count < 0))
        {
            throw new InvalidOperationException();
        }
        
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

        using var locks = _locks.Lock([.. order.Keys.OrderDescending()]);

        decimal total = 0;

        var skuCount = new Dictionary<long, int>();
        foreach (var item in order)
        {
            if (!_store.ContainsKey(item.Key) || item.Value > _store[item.Key].Count)
            {
                throw new InvalidOperationException($"Order {item.Key} does not exist");
            }
            
            total += item.Value * _store[item.Key].Price;
            if (!skuCount.ContainsKey(item.Key))
            {
                skuCount.Add(item.Key, _store[item.Key].Count -item.Value);
            }
            else
            {
                skuCount[item.Key] -= item.Value;
            }
        }

        foreach (var skuC in skuCount)
        {
            _store[skuC.Key].Count = skuC.Value;
        }

        total *= 1 - client.Discount * (decimal)0.01 ;
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
    
    private sealed class SkuLocks
    {
        private readonly ConcurrentDictionary<long, object> _locks = new();
        
        public IDisposable Lock(IReadOnlyCollection<long> keys)
        {
            var objects = new List<object>();
            foreach (var key in keys.OrderDescending())
            {
                var objLock = _locks.GetOrAdd(key, _ => new object());
                Monitor.Enter(objLock);
                objects.Add(objLock);
            }
            return new Releaser(objects);
        }

        private struct Releaser : IDisposable
        {
            private readonly IReadOnlyCollection<object> _toRelease;
            
            public Releaser(IReadOnlyCollection<object> toRelease) => _toRelease = toRelease;

            public void Dispose()
            {
                foreach (var item in _toRelease)
                {
                    Monitor.Exit(item);
                }
            }
        }
    }
}