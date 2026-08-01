using System.Threading;

namespace BattleLuck.Services.AI;

/// <summary>
/// Defines concurrency and permission policies for AI requests.
/// Kept intentionally small and readable.
/// </summary>
internal static class AiRequestPolicy
{
    /// <summary>Maximum player-controlled text accepted by any AI entry point.</summary>
    public const int MaxCharacters = 2048;

    /// <summary>
    /// Max concurrent AI queries on the server thread.
    /// </summary>
    public static int MaxConcurrentDirectQueries { get; set; } = 4;

    /// <summary>
    /// Timeout in seconds before an AI request is cancelled.
    /// </summary>
    public static int RequestTimeoutSeconds { get; set; } = 90;

    /// <summary>
    /// Cooldown in seconds between AI messages from the same player.
    /// </summary>
    public static int MessageCooldownSeconds { get; set; } = 30;

    /// <summary>
    /// Validate and normalize player-controlled text before an action router,
    /// sidecar, or model provider can inspect it.
    /// </summary>
    public static bool TryValidate(string? value, out string normalized, out string reason)
    {
        normalized = (value ?? string.Empty).Trim();
        reason = string.Empty;

        if (normalized.Length == 0)
        {
            reason = "AI request cannot be empty.";
            return false;
        }

        if (normalized.Length > MaxCharacters)
        {
            reason = $"AI request is too long. Maximum: {MaxCharacters} characters.";
            return false;
        }

        foreach (var character in normalized)
        {
            if (!char.IsControl(character) || character is '\r' or '\n' or '\t')
                continue;

            reason = "AI request contains unsupported control characters.";
            normalized = string.Empty;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Apply the shared text boundary and require a live player entity for
    /// direct in-game requests.
    /// </summary>
    public static bool TryValidate(Entity player, string message, out string reason)
    {
        reason = string.Empty;
        if (!player.Exists() || !player.IsPlayer())
        {
            reason = "Target must be a live player.";
            return false;
        }

        if (!TryValidate(message, out var normalized, out reason))
            return false;
        if (normalized.Length < 2)
        {
            reason = "AI request must contain at least two characters.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Create a SemaphoreSlim based on the current policy values.
    /// </summary>
    public static SemaphoreSlim CreateConcurrencyGate()
    {
        return new SemaphoreSlim(MaxConcurrentDirectQueries, MaxConcurrentDirectQueries);
    }
}
