using Microsoft.AspNetCore.SignalR;
using SignalRRealtime.Api.Hubs;
using SignalRRealtime.Api.Models;
using SignalRRealtime.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var signalR = builder.Services.AddSignalR(options =>
{
    // Detailed errors are useful while learning. Keep them off outside
    // development so exception internals do not leak to clients.
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.MaximumReceiveMessageSize = 32 * 1024;
});

var redisConnectionString = builder.Configuration["SignalR:RedisConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    // Multi-server scale-out: Redis carries messages between app servers.
    // See docs/scaling.md before enabling this in production.
    signalR.AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix =
            StackExchange.Redis.RedisChannel.Literal("signalr-realtime");
    });
}

builder.Services.AddSingleton<QuoteStore>();
builder.Services.AddSingleton<RoomPresenceTracker>();
builder.Services.AddHostedService<MarketTickerService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("BrowserClients", policy => policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

app.UseCors("BrowserClients");
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "signalr-realtime",
    utc = DateTime.UtcNow
}));

app.MapGet("/api/quotes", (QuoteStore quotes) => Results.Ok(quotes.GetSnapshot()));

app.MapPost("/api/broadcast", async (
    BroadcastRequest request,
    IHubContext<RealtimeHub, IRealtimeClient> hub,
    CancellationToken cancellationToken) =>
{
    try
    {
        var message = InputSanitizer.NormalizeMessage(request.Message);
        await hub.Clients.All.ReceiveNotification(message);
        return Results.Accepted(value: new { sentUtc = DateTime.UtcNow });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapHub<RealtimeHub>("/hubs/realtime");
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program
{
}
