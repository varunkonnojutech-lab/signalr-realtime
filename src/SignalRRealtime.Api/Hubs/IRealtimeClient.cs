using SignalRRealtime.Api.Models;

namespace SignalRRealtime.Api.Hubs;

/// <summary>
/// The server-to-client contract. A strongly-typed hub turns method-name
/// typos into compiler errors instead of messages silently sent nowhere.
/// </summary>
public interface IRealtimeClient
{
    Task ReceiveMessage(ChatMessage message);
    Task ReceiveQuote(StockQuote quote);
    Task UserJoined(string room, string userName, int userCount);
    Task UserLeft(string room, string userName, int userCount);
    Task ReceiveNotification(string message);
}
