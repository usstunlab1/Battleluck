using BattleLuck.Models;

namespace BattleLuck.Utilities;

/// <summary>
/// Read-only, bidirectional database for the V Rising prefab archive.  It is
/// deliberately separate from the live ECS registry so it can also be used by
/// action parsing before a server world is available.
/// </summary>
public sealed class PrefabCatalog
{
    readonly Dictionary<int, PrefabArchiveEntry> _byGuid = new();
    readonly Dictionary<string, int> _byName = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byGuid.Count;

    public static PrefabCatalog Load(string path)
    {
        var catalog = new PrefabCatalog();
        if (!File.Exists(path))
            return catalog;

        var archive = JsonSerializer.Deserialize<PrefabArchive>(File.ReadAllText(path));
        if (archive == null)
            return catalog;

        foreach (var (hashText, entry) in archive.Prefabs)
        {
            if (!int.TryParse(hashText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hash) ||
                string.IsNullOrWhiteSpace(entry.Name))
                continue;

            catalog._byGuid[hash] = entry;
            catalog._byName.TryAdd(entry.Name, hash);
        }

        return catalog;
    }

    public bool TryGetGuid(string name, out PrefabGUID guid)
    {
        if (_byName.TryGetValue(name.Trim(), out var hash))
        {
            guid = new PrefabGUID(hash);
            return true;
        }

        guid = PrefabGUID.Empty;
        return false;
    }

    public bool TryGetName(PrefabGUID guid, out string name)
    {
        if (_byGuid.TryGetValue(guid.GuidHash, out var entry))
        {
            name = entry.Name;
            return true;
        }

        name = string.Empty;
        return false;
    }

    /// <summary>
    /// Returns immutable archive metadata for a resolved prefab.  Callers must
    /// still resolve and validate the prefab against the live server registry
    /// before using it in ECS.
    /// </summary>
    public bool TryGetEntry(PrefabGUID guid, out PrefabArchiveEntry entry)
    {
        if (_byGuid.TryGetValue(guid.GuidHash, out var found))
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    public bool TryGetEntry(string name, out PrefabArchiveEntry entry)
    {
        if (_byName.TryGetValue(name.Trim(), out var hash) &&
            _byGuid.TryGetValue(hash, out var found))
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    public IEnumerable<(PrefabGUID Guid, PrefabArchiveEntry Entry)> Search(string? query, int limit = 100)
    {
        var text = (query ?? string.Empty).Trim();
        return _byGuid
            .Where(pair => text.Length == 0 || pair.Value.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(pair => (new PrefabGUID(pair.Key), pair.Value));
    }

    public IEnumerable<PrefabActionExport> ExportActions()
        => _byGuid.OrderBy(pair => pair.Key).Select(pair => new PrefabActionExport
        {
            Action = "prefab.resolve",
            Prefab = pair.Value.Name,
            PrefabGuid = pair.Key,
            Category = pair.Value.Category,
            Description = $"Resolve {pair.Value.Name} to V Rising prefab GUID {pair.Key}."
        });
}

/// <summary>Portable action representation emitted by the prefab database.</summary>
public sealed class PrefabActionExport
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = "prefab.resolve";

    [JsonPropertyName("prefab")]
    public string Prefab { get; init; } = string.Empty;

    [JsonPropertyName("prefabGuid")]
    public int PrefabGuid { get; init; }

    [JsonPropertyName("category")]
    public string Category { get; init; } = "Other";

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}
