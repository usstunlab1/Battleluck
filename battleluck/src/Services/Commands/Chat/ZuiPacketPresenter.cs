using System.Collections.Concurrent;
using System.Text.Json;

namespace BattleLuck.Services.Chat;

public sealed record ZuiButton(string Text, string Command);
public sealed record ZuiWindow(string Id, string Title, IReadOnlyList<string> Lines, IReadOnlyList<ZuiButton> Buttons);

/// <summary>Server-only serializer for the optional ZUI client packet convention.</summary>
public sealed class ZuiPacketPresenter
{
    const string Prefix = "[[ZUI]]";
    // V Rising system messages are carried by FixedString512Bytes. Keep the
    // complete ZUI envelope below NotificationHelper's UTF-8 safety boundary.
    const int MaxPacketUtf8Bytes = NotificationHelper.MaxSystemMessageUtf8Bytes;
    const int MaxLines = 30;
    const int MaxButtons = 12;
    readonly ConcurrentDictionary<ulong, byte> _optedIn = new();
    readonly Action<ulong, string> _sendPrivate;

    public ZuiPacketPresenter(Action<ulong, string> sendPrivate) => _sendPrivate = sendPrivate;
    public bool IsEnabled(ulong steamId) => _optedIn.ContainsKey(steamId);
    public void Enable(ulong steamId) => _optedIn[steamId] = 0;
    public void Disable(ulong steamId) => _optedIn.TryRemove(steamId, out _);
    public void Clear() => _optedIn.Clear();

    public bool TrySend(ulong steamId, ZuiWindow window)
    {
        if (!IsEnabled(steamId) || string.IsNullOrWhiteSpace(window.Id)) return false;
        var safeButtons = window.Buttons.Take(MaxButtons).Where(button => IsViewOnlyCommand(button.Command))
            .Select(button => new { text = Plain(button.Text, 80), command = Plain(button.Command, 180) }).ToArray();
        var packet = Prefix + JsonSerializer.Serialize(new
        {
            type = "window",
            id = Plain(window.Id, 80),
            title = Plain(window.Title, 120),
            lines = window.Lines.Take(MaxLines).Select(line => Plain(line, 300)).ToArray(),
            buttons = safeButtons
        });
        if (System.Text.Encoding.UTF8.GetByteCount(packet) > MaxPacketUtf8Bytes) return false;
        _sendPrivate(steamId, packet);
        return true;
    }

    /// <summary>
    /// ZUI is a view transport, not an action surface. Only closed UI navigation
    /// routes may be serialized into a clickable button.
    /// </summary>
    public static bool IsViewOnlyCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        var parts = command.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3 ||
            !parts[0].Equals(".ai", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].Equals("ui", StringComparison.OrdinalIgnoreCase))
            return false;

        return parts.Length == 2 || BattleLuckZuiDashboard.IsKnownRoute(parts[2]);
    }

    static string Plain(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var clean = value.Replace("<", "‹", StringComparison.Ordinal).Replace(">", "›", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        return clean.Length <= max ? clean : clean[..max];
    }
}
