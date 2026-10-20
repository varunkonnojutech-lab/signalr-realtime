using System.Collections.Concurrent;
using SignalRRealtime.Api.Models;

namespace SignalRRealtime.Api.Services;

public sealed class QuoteStore
{
    private readonly ConcurrentDictionary<string, StockQuote> _quotes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _updateLock = new();

    public QuoteStore()
    {
        var now = DateTime.UtcNow;
        Seed(new StockQuote("AAPL", "Apple", 227.40m, 0m, now));
        Seed(new StockQuote("AMZN", "Amazon", 197.85m, 0m, now));
        Seed(new StockQuote("MSFT", "Microsoft", 510.25m, 0m, now));
        Seed(new StockQuote("NVDA", "NVIDIA", 131.20m, 0m, now));
    }

    public IReadOnlyList<StockQuote> GetSnapshot() =>
        _quotes.Values.OrderBy(quote => quote.Symbol).ToList();

    public IReadOnlyList<StockQuote> ApplyRandomWalk(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        lock (_updateLock)
        {
            foreach (var symbol in _quotes.Keys.ToList())
            {
                var current = _quotes[symbol];
                // A small, biased random walk keeps the dashboard lively while
                // making the direction deterministic enough to reason about.
                var movement = (decimal)(random.NextDouble() - 0.47) * 0.006m;
                var newPrice = Math.Max(1m, Math.Round(current.Price * (1 + movement), 2));
                var changePercent = Math.Round((newPrice - current.Price) / current.Price * 100m, 3);

                _quotes[symbol] = current with
                {
                    Price = newPrice,
                    ChangePercent = changePercent,
                    UpdatedUtc = DateTime.UtcNow
                };
            }

            return GetSnapshot();
        }
    }

    private void Seed(StockQuote quote) => _quotes[quote.Symbol] = quote;
}
