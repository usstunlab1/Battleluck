using BattleLuck.Models;
using BattleLuck.Utilities;

namespace BattleLuck.Services.Runtime;

/// <summary>
/// Owns terminal records for BattleLuck sessions.  It deliberately has no API
/// to restore a world or player state: player snapshots and host world saves
/// have separate, authoritative recovery paths.
/// </summary>
public sealed class SessionArchiveService
{
    static readonly JsonSerializerOptions JsonOptions = new(ConfigLoader.JsonOptions)
    {
        WriteIndented = true
    };

    readonly string _root;

    public SessionArchiveService(string? root = null) =>
        _root = Path.GetFullPath(root ?? Path.Combine(ConfigLoader.ConfigRoot, "runtime", "session-archive"));

    public string Root => _root;

    public OperationResult<SessionArchiveRecord> Archive(SessionArchiveRecord record)
    {
        if (record == null)
            return OperationResult<SessionArchiveRecord>.Fail("Session archive record is required.");

        try
        {
            record.Version = 1;
            record.Reason = NormalizeReason(record.Reason);
            record.ArchiveId = NewArchiveId();
            record.EndedUtc = record.EndedUtc == default ? DateTime.UtcNow : record.EndedUtc;
            var path = GetPath(record.ArchiveId);
            SafeFileSystem.WriteAllTextAtomic(path, JsonSerializer.Serialize(record, JsonOptions));
            return OperationResult<SessionArchiveRecord>.Ok(record);
        }
        catch (Exception ex)
        {
            return OperationResult<SessionArchiveRecord>.Fail($"Session archive write failed: {ex.Message}");
        }
    }

    public IReadOnlyList<SessionArchiveRecord> List(int limit = 30)
    {
        if (!Directory.Exists(_root))
            return Array.Empty<SessionArchiveRecord>();

        var result = new List<SessionArchiveRecord>();
        foreach (var path in Directory.EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Take(Math.Clamp(limit, 1, 200)))
        {
            try
            {
                var record = JsonSerializer.Deserialize<SessionArchiveRecord>(File.ReadAllText(path), JsonOptions);
                if (record != null && SafeFileSystem.IsSafeIdentifier(record.ArchiveId))
                    result.Add(record);
            }
            catch
            {
                // A corrupt historical record must not prevent an operator
                // from seeing later valid teardown evidence.
            }
        }
        return result;
    }

    public OperationResult Delete(string archiveId, bool confirmed)
    {
        if (!confirmed)
            return OperationResult.Fail("Deletion requires the exact final 'confirm' token.");
        if (!SafeFileSystem.IsSafeIdentifier(archiveId))
            return OperationResult.Fail("Invalid archive id.");

        try
        {
            var path = GetPath(archiveId);
            if (!File.Exists(path))
                return OperationResult.Fail($"Session archive '{archiveId}' was not found.");
            File.Delete(path);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"Session archive deletion failed: {ex.Message}");
        }
    }

    string GetPath(string archiveId) =>
        SafeFileSystem.CombineUnderRoot(_root, SafeFileSystem.RequireSafeIdentifier(archiveId, nameof(archiveId)) + ".json");

    static string NewArchiveId() =>
        $"session-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

    static string NormalizeReason(string? reason) => reason?.Trim().ToLowerInvariant() switch
    {
        "destroy" => "destroy",
        "rollback" => "rollback",
        "completed" => "completed",
        "shutdown" => "shutdown",
        _ => "completed"
    };
}
