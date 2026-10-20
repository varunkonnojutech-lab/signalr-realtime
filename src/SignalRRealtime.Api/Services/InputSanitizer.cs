namespace SignalRRealtime.Api.Services;

/// <summary>
/// Hub methods are public network endpoints. Treat their arguments with the
/// same suspicion as an HTTP request body: trim, bound, and normalize them in
/// one place instead of trusting whatever a client sends.
/// </summary>
public static class InputSanitizer
{
    public const int MaxMessageLength = 500;

    public static string NormalizeRoom(string? room)
    {
        if (string.IsNullOrWhiteSpace(room))
            throw new ArgumentException("A room name is required.", nameof(room));

        var normalized = room.Trim().ToLowerInvariant().Replace(' ', '-');
        // Reject rather than truncate: silently shortening a name would route
        // the caller to a different room than the one they asked for.
        if (normalized.Length > 40)
            throw new ArgumentException("Room names are limited to 40 characters.", nameof(room));

        if (normalized.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_')))
            throw new ArgumentException("Room names may contain letters, numbers, hyphens, and underscores.", nameof(room));

        return normalized;
    }

    public static string NormalizeUserName(string? userName, string connectionId)
    {
        var fallback = $"guest-{connectionId[..Math.Min(6, connectionId.Length)]}";
        if (string.IsNullOrWhiteSpace(userName))
            return fallback;

        var normalized = userName.Trim();
        return normalized.Length <= 40 ? normalized : normalized[..40];
    }

    public static string NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A message is required.", nameof(message));

        var normalized = message.Trim();
        if (normalized.Length > MaxMessageLength)
            throw new ArgumentException($"Messages are limited to {MaxMessageLength} characters.", nameof(message));

        return normalized;
    }
}
