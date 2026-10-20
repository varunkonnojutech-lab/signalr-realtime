using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using SignalRRealtime.Api.Models;
using Xunit;

namespace SignalRRealtime.Tests;

public sealed class SignalRIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SignalRIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HomePage_Serves_The_Realtime_Client()
    {
        var response = await _client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("SignalR Realtime Lab");
        html.Should().Contain("Live dashboard");
        html.Should().Contain("Room chat");
    }

    [Fact]
    public async Task QuotesEndpoint_Returns_The_Initial_Snapshot()
    {
        var quotes = await _client.GetFromJsonAsync<List<StockQuote>>("/api/quotes");

        quotes.Should().NotBeNull();
        quotes!.Should().HaveCount(4);
        quotes!.Select(quote => quote.Symbol)
            .Should().Equal("AAPL", "AMZN", "MSFT", "NVDA");
    }

    [Fact]
    public async Task BroadcastEndpoint_Validates_Empty_Messages()
    {
        var response = await _client.PostAsJsonAsync("/api/broadcast", new BroadcastRequest(" "));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Two_Clients_In_The_Same_Room_Can_Chat()
    {
        var room = $"test-{Guid.NewGuid():N}";
        await using var sender = CreateConnection();
        await using var receiver = CreateConnection();

        var received = new TaskCompletionSource<ChatMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.On<ChatMessage>("ReceiveMessage", message => received.TrySetResult(message));

        await sender.StartAsync();
        await receiver.StartAsync();

        await sender.InvokeAsync("JoinRoom", room, "Sender");
        await receiver.InvokeAsync("JoinRoom", room, "Receiver");
        await sender.InvokeAsync("SendMessage", room, "Sender", "Hello from the hub");

        var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        message.Room.Should().Be(room);
        message.UserName.Should().Be("Sender");
        message.Text.Should().Be("Hello from the hub");
        message.SenderConnectionId.Should().NotBeNullOrWhiteSpace();
    }

    private HubConnection CreateConnection()
    {
        var handler = _factory.Server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/realtime", options =>
            {
                options.HttpMessageHandlerFactory = _ => handler;
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }
}
