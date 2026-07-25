using System.Threading;

namespace BattleLuck.Services.AI;

/// <summary>
/// Defines concurrency and permission policies for AI requests.
/// Kept intentionally small and readable.
/// </summary>
internal static class AiRequestPolicy
{
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
    /// Create a SemaphoreSlim based on the current policy values.
    /// </summary>
    public static SemaphoreSlim CreateConcurrencyGate()
    {
        return new SemaphoreSlim(MaxConcurrentDirectQueries, MaxConcurrentDirectQueries);
    }
}