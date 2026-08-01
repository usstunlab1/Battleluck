using System.Text.Json;
using BattleLuck.Models;
using ProjectM.CastleBuilding;

namespace BattleLuck.Services.Castles;

// ─────────────────────────────────────────────────────────────────────────────
// CastlePolicyStore
//
// Schema-versioned JSON store for castle policies. Persisted to:
//   <BepInEx>/data/BattleLuck/castle_policies.json
//
// This is mutable server state, NOT static configuration. The store is the
// single source of truth: the service loads from it on startup, writes to
// it on every committed mutation, and never trusts in-memory state across
// restarts.
// ─────────────────────────────────────────────────────────────────────────────

public sealed class CastlePolicyStore
{
    const int CurrentSchemaVersion = 1;

    readonly object _sync = new();
    readonly string _path;
    readonly Action<string>? _warning;
    readonly Dictionary<string, CastleObjectPolicy> _byId = new(StringComparer.OrdinalIgnoreCase);
    bool _dirty;
    long _changeVersion;
    bool _disposed;

    public CastlePolicyStore(string path, Action<string>? warning = null)
    {
        _path = path;
        _warning = warning;
    }

    public int Count
    {
        get { lock (_sync) return _byId.Count; }
    }

    public void Load()
    {
        if (!File.Exists(_path))
        {
            MarkDirtyLocked();
            SaveNow();
            return;
        }
        try
        {
            var store = JsonSerializer.Deserialize<CastlePolicyStoreDocument>(File.ReadAllText(_path), JsonOptions())
                ?? new CastlePolicyStoreDocument();
            lock (_sync)
            {
                _byId.Clear();
                foreach (var policy in store.Policies.Where(p => !string.IsNullOrWhiteSpace(p.PolicyId)))
                    _byId[NormalizeId(policy.PolicyId)] = Clone(policy);
            }
        }
        catch (Exception ex)
        {
            var quarantine = _path + $".invalid-{DateTime.UtcNow:yyyyMMddHHmmss}";
            try { File.Move(_path, quarantine, overwrite: false); } catch { }
            _warning?.Invoke($"[CastlePolicy] Invalid store quarantined: {ex.Message}");
            MarkDirtyLocked();
            SaveNow();
        }
    }

    public void SaveNow()
    {
        CastlePolicyStoreDocument document;
        long savedVersion;
        lock (_sync)
        {
            if (!_dirty && File.Exists(_path)) return;
            document = new CastlePolicyStoreDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Policies = _byId.Values.Select(Clone).OrderBy(p => p.PolicyId).ToList()
            };
            savedVersion = _changeVersion;
        }
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(document, JsonOptions(writeIndented: true)));
            File.Move(tmp, _path, overwrite: true);
            lock (_sync)
            {
                if (_changeVersion == savedVersion) _dirty = false;
            }
        }
        catch (Exception ex)
        {
            _warning?.Invoke($"[CastlePolicy] Save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Atomic upsert. The provided policy is cloned before storage so the
    /// caller cannot mutate the persisted record after the call returns.
    /// </summary>
    public CastleObjectPolicy Upsert(CastleObjectPolicy policy)
    {
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        if (string.IsNullOrWhiteSpace(policy.PolicyId))
            throw new ArgumentException("policyId is required.", nameof(policy));

        var key = NormalizeId(policy.PolicyId);
        var snapshot = Clone(policy);
        snapshot.PolicyId = key;

        lock (_sync)
        {
            _byId[key] = snapshot;
            MarkDirtyLocked();
        }
        SaveNow();
        return Clone(snapshot);
    }

    public bool Remove(string policyId)
    {
        var key = NormalizeId(policyId);
        lock (_sync)
        {
            if (!_byId.Remove(key)) return false;
            MarkDirtyLocked();
        }
        SaveNow();
        return true;
    }

    public CastleObjectPolicy? Get(string policyId)
    {
        var key = NormalizeId(policyId);
        lock (_sync)
        {
            return _byId.TryGetValue(key, out var policy) ? Clone(policy) : null;
        }
    }

    public IReadOnlyList<CastleObjectPolicy> ListAll()
    {
        lock (_sync)
        {
            return _byId.Values.Select(Clone).OrderBy(p => p.PolicyId).ToList();
        }
    }

    public IReadOnlyList<CastleObjectPolicy> ListByOwner(ulong ownerSteamId)
    {
        lock (_sync)
        {
            return _byId.Values
                .Where(p => p.OwnerSteamId == ownerSteamId)
                .OrderBy(p => p.Kind).ThenBy(p => p.PolicyId)
                .Select(Clone)
                .ToList();
        }
    }

    public IReadOnlyList<CastleObjectPolicy> ListByKind(CastleObjectKind kind)
    {
        lock (_sync)
        {
            return _byId.Values
                .Where(p => p.Kind == kind)
                .OrderBy(p => p.OwnerName).ThenBy(p => p.PolicyId)
                .Select(Clone)
                .ToList();
        }
    }

    public IReadOnlyList<CastleObjectPolicy> FindByTarget(CastleObjectKey key)
    {
        if (key == null) return Array.Empty<CastleObjectPolicy>();
        lock (_sync)
        {
            return _byId.Values
                .Where(p => SameTarget(p.Target, key))
                .Select(Clone)
                .ToList();
        }
    }

    static bool SameTarget(CastleObjectKey a, CastleObjectKey b) =>
        a.OwnerSteamId == b.OwnerSteamId
        && a.ObjectPrefabHash == b.ObjectPrefabHash
        && a.CastleHeartPrefabHash == b.CastleHeartPrefabHash
        && a.MapIndex == b.MapIndex
        && Math.Abs(a.LocalPosition.X - b.LocalPosition.X) < 0.5f
        && Math.Abs(a.LocalPosition.Y - b.LocalPosition.Y) < 0.5f
        && Math.Abs(a.LocalPosition.Z - b.LocalPosition.Z) < 0.5f;

    public CastleObjectPolicy Clone(CastleObjectPolicy p) => new()
    {
        PolicyId = p.PolicyId,
        Target = Clone(p.Target),
        OwnerSteamId = p.OwnerSteamId,
        OwnerName = p.OwnerName,
        Kind = p.Kind,
        Access = p.Access,
        Schedule = Clone(p.Schedule),
        Cost = Clone(p.Cost),
        Quota = Clone(p.Quota),
        Permissions = p.Permissions.Select(Clone).ToList(),
        QuotaCounters = p.QuotaCounters.Select(Clone).ToList(),
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc,
        ExcludeFromTerritoryApply = p.ExcludeFromTerritoryApply,
        Label = p.Label
    };

    public static CastleObjectKey Clone(CastleObjectKey k) => new()
    {
        OwnerSteamId = k.OwnerSteamId,
        CastleHeartPrefabHash = k.CastleHeartPrefabHash,
        ObjectPrefabHash = k.ObjectPrefabHash,
        MapIndex = k.MapIndex,
        LocalPosition = new QuantizedPosition { X = k.LocalPosition.X, Y = k.LocalPosition.Y, Z = k.LocalPosition.Z }
    };

    public static CastleAccessSchedule Clone(CastleAccessSchedule s) => new()
    {
        Mode = s.Mode,
        Windows = s.Windows.Select(w => new CastleHoursWindow { StartHour = w.StartHour, EndHour = w.EndHour }).ToList()
    };

    public static CastleAccessCost Clone(CastleAccessCost c) => new()
    {
        Kind = c.Kind,
        PrefabHash = c.PrefabHash,
        PrefabName = c.PrefabName,
        Amount = c.Amount,
        PaymentTarget = c.PaymentTarget != null ? Clone(c.PaymentTarget) : null
    };

    public static CastleUsageQuota Clone(CastleUsageQuota q) => new()
    {
        Kind = q.Kind,
        MaxAmount = q.MaxAmount,
        WindowHours = q.WindowHours
    };

    public static CastlePermissionRule Clone(CastlePermissionRule p) => new()
    {
        SubjectSteamId = p.SubjectSteamId,
        SubjectName = p.SubjectName,
        ClanTag = p.ClanTag,
        Effect = p.Effect,
        GrantedAtUtc = p.GrantedAtUtc
    };

    public static CastleQuotaCounter Clone(CastleQuotaCounter c) => new()
    {
        SubjectSteamId = c.SubjectSteamId,
        Count = c.Count,
        WindowStartUtc = c.WindowStartUtc,
        TotalCount = c.TotalCount
    };

    void MarkDirtyLocked()
    {
        _dirty = true;
        _changeVersion++;
    }

    public static string NormalizeId(string value) =>
        new((value ?? "").Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray());

    static JsonSerializerOptions JsonOptions(bool writeIndented = false) => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = writeIndented
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SaveNow();
    }

    public sealed class CastlePolicyStoreDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;
        [JsonPropertyName("policies")]
        public List<CastleObjectPolicy> Policies { get; set; } = new();
    }
}
