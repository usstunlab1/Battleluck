using System.Text.Json.Serialization;

namespace BattleLuck.Models;

/// <summary>
/// Small, non-sensitive terminal record for a BattleLuck-owned live session.
/// This is evidence of teardown, not a V Rising world-save backup and never
/// contains player inventories, Steam IDs, or authentication data.
/// </summary>
public sealed class SessionArchiveRecord
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("archiveId")] public string ArchiveId { get; set; } = "";
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = "";
    [JsonPropertyName("modeId")] public string ModeId { get; set; } = "";
    [JsonPropertyName("zoneHash")] public int ZoneHash { get; set; }
    [JsonPropertyName("reason")] public string Reason { get; set; } = "completed";
    [JsonPropertyName("createdUtc")] public DateTime CreatedUtc { get; set; }
    [JsonPropertyName("endedUtc")] public DateTime EndedUtc { get; set; }
    [JsonPropertyName("wasStarted")] public bool WasStarted { get; set; }
    [JsonPropertyName("participantCount")] public int ParticipantCount { get; set; }
    [JsonPropertyName("blueprintActiveCount")] public int BlueprintActiveCount { get; set; }
    [JsonPropertyName("blueprintPendingCount")] public int BlueprintPendingCount { get; set; }
    [JsonPropertyName("blueprintMissingCount")] public int BlueprintMissingCount { get; set; }
    [JsonPropertyName("blueprintFailed")] public bool BlueprintFailed { get; set; }
}
