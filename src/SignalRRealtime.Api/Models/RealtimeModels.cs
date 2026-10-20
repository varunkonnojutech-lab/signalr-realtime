namespace SignalRRealtime.Api.Models;

public sealed record ChatMessage(
    string Room,
    string UserName,
    string Text,
    DateTime SentUtc,
    string SenderConnectionId);

public sealed record StockQuote(
    string Symbol,
    string Company,
    decimal Price,
    decimal ChangePercent,
    DateTime UpdatedUtc);

public sealed record BroadcastRequest(string Message);

public sealed record PresenceChanged(string Room, string UserName, int UserCount);
