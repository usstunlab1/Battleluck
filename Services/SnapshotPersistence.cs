using System.Text.Json;
using BattleLuck.Models;

namespace BattleLuck.Services;

/// <summary>
/// File-based persistence for <see cref="PlayerSnapshot"/> that is independent of Unity/BepInEx runtime.
/// Produces two files per Steam ID:
/// - "{steamId}_event.json" when the snapshot is captured inside an event (zoneHash > 0)
/// - "{steamId}_regular.json" for regular play (zoneHash <= 0)
/// Also supports a backward-compatibility fallback to old-style "{steamId}.json" files.
/// </summary>
public static class SnapshotPersistence
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static string GetBepInExRootPath()
    {
        try
        {
            return BepInEx.Paths.BepInExRootPath ?? AppContext.BaseDirectory;
        }
        catch
        {
            return AppContext.BaseDirectory;
        }
    }

    static readonly string SnapshotDir = Path.Combine(GetBepInExRootPath(), "data", "BattleLuck", "snapshots");

    public static string DirectoryPath => SnapshotDir;

    public static string GetPath(ulong steamId, bool isEvent)
        => Path.Combine(SnapshotDir, isEvent ? $"{steamId}_event.json" : $"{steamId}_regular.json");

    public static bool Exists(ulong steamId, bool isEvent)
        => File.Exists(GetPath(steamId, isEvent));

    public static void Write(ulong steamId, PlayerSnapshot snapshot, bool isEvent)
    {
        Directory.CreateDirectory(SnapshotDir);
        var json = JsonSerializer.Serialize(snapshot, JsonOpts);
        File.WriteAllText(GetPath(steamId, isEvent), json);
    }

    public static PlayerSnapshot? Read(ulong steamId, bool isEvent)
    {
        var path = GetPath(steamId, isEvent);
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PlayerSnapshot>(json, JsonOpts);
        }

        // Fallback to legacy single file {steamId}.json, but only when it matches requested kind
        var oldPath = Path.Combine(SnapshotDir, $"{steamId}.json");
        if (!File.Exists(oldPath)) return null;

        try
        {
            var json = File.ReadAllText(oldPath);
            var snap = JsonSerializer.Deserialize<PlayerSnapshot>(json, JsonOpts);
            if (snap == null) return null;
            var oldIsEvent = snap.ZoneHash > 0;
            return oldIsEvent == isEvent ? snap : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Delete(ulong steamId, bool isEvent)
    {
        var path = GetPath(steamId, isEvent);
        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { /* ignore */ }
        }

        // Also remove legacy file if it corresponds to the same category
        var oldPath = Path.Combine(SnapshotDir, $"{steamId}.json");
        if (!File.Exists(oldPath)) return;
        try
        {
            var json = File.ReadAllText(oldPath);
            var snap = JsonSerializer.Deserialize<PlayerSnapshot>(json, JsonOpts);
            if (snap == null) return;
            var oldIsEvent = snap.ZoneHash > 0;
            if (oldIsEvent == isEvent)
                File.Delete(oldPath);
        }
        catch { /* ignore */ }
    }

    public static IReadOnlyList<PlayerSnapshot> ListAll()
    {
        Directory.CreateDirectory(SnapshotDir);
        var list = new List<PlayerSnapshot>();
        foreach (var file in Directory.GetFiles(SnapshotDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var snap = JsonSerializer.Deserialize<PlayerSnapshot>(json, JsonOpts);
                if (snap != null)
                    list.Add(snap);
            }
            catch
            {
                // ignore unreadable files
            }
        }
        return list;
    }
}
