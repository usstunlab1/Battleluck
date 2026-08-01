using System.Text.Json;
using BattleLuck.Core;

namespace BattleLuck.Services.Runtime;

public sealed class BattleLuckHealthSnapshot
{
    public string ContractName { get; init; } = "battleluck.health";
    public string SchemaVersion { get; init; } = "1.0.0";
    public string Producer { get; init; } = "BattleLuck";
    public string ProducerVersion { get; init; } = BattleLuckPluginInfo.PluginVersion;
    public string Status { get; init; } = "starting";
    public bool Initialized { get; init; }
    public bool WorldReady { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
}

/// <summary>
/// Publishes a bounded, secret-free health contract beneath BattleLuck's own
/// runtime configuration directory. Consumers may read it, but never write it.
/// </summary>
public static class RuntimeHealthService
{
    static readonly object Gate = new();
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    static DateTime _nextPublishUtc;
    static string _lastStatus = "";

    public static BattleLuckHealthSnapshot CreateSnapshot(
        string status,
        bool initialized,
        bool worldReady,
        DateTime generatedAtUtc)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "unavailable" : status.Trim().ToLowerInvariant();
        return new BattleLuckHealthSnapshot
        {
            Status = normalized,
            Initialized = initialized,
            WorldReady = worldReady,
            GeneratedAtUtc = generatedAtUtc,
            ExpiresAtUtc = generatedAtUtc.AddSeconds(15)
        };
    }

    public static void Publish(string status, bool initialized, bool worldReady, bool force = false)
    {
        var now = DateTime.UtcNow;
        var normalized = string.IsNullOrWhiteSpace(status) ? "unavailable" : status.Trim().ToLowerInvariant();
        lock (Gate)
        {
            if (!force && now < _nextPublishUtc && string.Equals(_lastStatus, normalized, StringComparison.Ordinal))
                return;
            _nextPublishUtc = now.AddSeconds(5);
            _lastStatus = normalized;
        }

        try
        {
            var runtimeRoot = Path.GetFullPath(Path.Combine(ConfigLoader.ConfigRoot, "runtime"));
            var configRoot = Path.GetFullPath(ConfigLoader.ConfigRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!runtimeRoot.StartsWith(configRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Resolved health path escaped the BattleLuck configuration root.");

            Directory.CreateDirectory(runtimeRoot);
            var snapshot = CreateSnapshot(normalized, initialized, worldReady, now);
            SafeFileSystem.WriteAllTextAtomic(
                Path.Combine(runtimeRoot, "health.json"),
                JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception exception)
        {
            BattleLuckPlugin.LogWarning($"[Health] Could not publish runtime health: {exception.Message}");
        }
    }
}
