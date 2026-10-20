using System.Collections.Concurrent;
using SignalRRealtime.Api.Models;

namespace SignalRRealtime.Api.Services;

/// <summary>
/// In-memory room membership for this process. SignalR groups route messages;
/// this tracker answers the separate product question: "who is here?"
/// In a multi-server deployment this state belongs in Redis/database storage —
/// group membership itself is handled by the backplane.
/// </summary>
public sealed class RoomPresenceTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _rooms =
        new(StringComparer.OrdinalIgnoreCase);

    public int Join(string room, string connectionId, string userName)
    {
        var members = _rooms.GetOrAdd(room, _ => new ConcurrentDictionary<string, string>());
        members[connectionId] = userName;
        return members.Count;
    }

    public int Leave(string room, string connectionId)
    {
        if (!_rooms.TryGetValue(room, out var members))
            return 0;

        members.TryRemove(connectionId, out _);
        return members.Count;
    }

    public IReadOnlyList<PresenceChanged> LeaveAll(string connectionId)
    {
        var changes = new List<PresenceChanged>();

        foreach (var (room, members) in _rooms)
        {
            if (members.TryRemove(connectionId, out var userName))
                changes.Add(new PresenceChanged(room, userName, members.Count));
        }

        return changes;
    }

    public bool IsMember(string room, string connectionId) =>
        _rooms.TryGetValue(room, out var members) && members.ContainsKey(connectionId);

    public int GetUserCount(string room) =>
        _rooms.TryGetValue(room, out var members) ? members.Count : 0;

    public IReadOnlyList<string> GetUsers(string room)
    {
        if (!_rooms.TryGetValue(room, out var members))
            return Array.Empty<string>();

        return members.Values.OrderBy(name => name).ToList();
    }
}
