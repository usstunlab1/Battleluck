using System.Text.Json.Serialization;

namespace BattleLuck.Models;

/// <summary>
/// Audit record for event deployment operations, matching the EventDeploymentAuditRecord JSON schema.
/// </summary>
public sealed class EventDeploymentAuditRecord
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = "";

    [JsonPropertyName("gist")]
    public string? Gist { get; set; }

    [JsonPropertyName("files")]
    public Dictionary<string, bool> Files { get; set; } = new();

    [JsonPropertyName("fileHashes")]
    public Dictionary<string, string> FileHashes { get; set; } = new();

    [JsonPropertyName("validation")]
    public DeploymentValidation Validation { get; set; } = new();

    [JsonPropertyName("server")]
    public DeploymentServerStatus Server { get; set; } = new();

    [JsonPropertyName("rollback")]
    public bool Rollback { get; set; }

    [JsonPropertyName("exit")]
    public int Exit { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("backup")]
    public string? Backup { get; set; }

    [JsonPropertyName("restoredPlayers")]
    public int RestoredPlayers { get; set; }

    [JsonPropertyName("skippedPlayers")]
    public int SkippedPlayers { get; set; }
}

public sealed class DeploymentValidation
{
    [JsonPropertyName("json")]
    public bool Json { get; set; }

    [JsonPropertyName("schema")]
    public bool Schema { get; set; }

    [JsonPropertyName("ids")]
    public bool Ids { get; set; }
}

public sealed class DeploymentServerStatus
{
    [JsonPropertyName("registerOk")]
    public bool RegisterOk { get; set; }

    [JsonPropertyName("startOk")]
    public bool StartOk { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
