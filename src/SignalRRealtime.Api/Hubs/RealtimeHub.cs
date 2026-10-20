using Microsoft.AspNetCore.SignalR;
using SignalRRealtime.Api.Models;
using SignalRRealtime.Api.Services;

namespace SignalRRealtime.Api.Hubs;

public sealed class RealtimeHub : Hub<IRealtimeClient>
{
    public const string DashboardRoom = "dashboard";

    private readonly RoomPresenceTracker _presence;

    public RealtimeHub(RoomPresenceTracker presence)
    {
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.ReceiveNotification("Connected to the realtime hub.");
        await base.OnConnectedAsync();
    }

    public async Task JoinRoom(string room, string userName)
    {
        var normalizedRoom = NormalizeRoomOrThrow(room);
        var normalizedUser = InputSanitizer.NormalizeUserName(userName, Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, normalizedRoom);
        var count = _presence.Join(normalizedRoom, Context.ConnectionId, normalizedUser);

        await Clients.Group(normalizedRoom)
            .UserJoined(normalizedRoom, normalizedUser, count);
    }

    public async Task LeaveRoom(string room, string userName)
    {
        var normalizedRoom = NormalizeRoomOrThrow(room);
        var normalizedUser = InputSanitizer.NormalizeUserName(userName, Context.ConnectionId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, normalizedRoom);
        var count = _presence.Leave(normalizedRoom, Context.ConnectionId);

        await Clients.Group(normalizedRoom)
            .UserLeft(normalizedRoom, normalizedUser, count);
    }

    public async Task SendMessage(string room, string userName, string message)
    {
        var normalizedRoom = NormalizeRoomOrThrow(room);
        var normalizedUser = InputSanitizer.NormalizeUserName(userName, Context.ConnectionId);

        if (!_presence.IsMember(normalizedRoom, Context.ConnectionId))
            throw new HubException("Join the room before sending a message.");

        string normalizedMessage;
        try
        {
            normalizedMessage = InputSanitizer.NormalizeMessage(message);
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }

        var chatMessage = new ChatMessage(
            normalizedRoom,
            normalizedUser,
            normalizedMessage,
            DateTime.UtcNow,
            Context.ConnectionId);

        await Clients.Group(normalizedRoom).ReceiveMessage(chatMessage);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var change in _presence.LeaveAll(Context.ConnectionId))
        {
            await Clients.Group(change.Room)
                .UserLeft(change.Room, change.UserName, change.UserCount);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string NormalizeRoomOrThrow(string room)
    {
        try
        {
            return InputSanitizer.NormalizeRoom(room);
        }
        catch (ArgumentException ex)
        {
            throw new HubException(ex.Message);
        }
    }
}
