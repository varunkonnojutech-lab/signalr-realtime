using Microsoft.AspNetCore.SignalR;
using SignalRRealtime.Api.Hubs;

namespace SignalRRealtime.Api.Services;

/// <summary>
/// Simulates a market-data feed. A production version would subscribe to a
/// queue/feed handler and publish the same strongly-typed quote messages.
/// </summary>
public sealed class MarketTickerService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(3);

    private readonly IHubContext<RealtimeHub, IRealtimeClient> _hub;
    private readonly QuoteStore _quotes;
    private readonly ILogger<MarketTickerService> _logger;
    private readonly Random _random = new();

    public MarketTickerService(
        IHubContext<RealtimeHub, IRealtimeClient> hub,
        QuoteStore quotes,
        ILogger<MarketTickerService> logger)
    {
        _hub = hub;
        _quotes = quotes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Market ticker started with a {IntervalSeconds}-second interval.",
            TickInterval.TotalSeconds);

        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var snapshot = _quotes.ApplyRandomWalk(_random);
                foreach (var quote in snapshot)
                {
                    await _hub.Clients
                        .Group(RealtimeHub.DashboardRoom)
                        .ReceiveQuote(quote);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Market ticker stopped.");
        }
    }
}
