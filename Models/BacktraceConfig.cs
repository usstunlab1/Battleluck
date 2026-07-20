using System.Text.Json.Serialization;

namespace BattleLuck.Models;

/// <summary>
/// Configuration for Backtrace error reporting service.
/// See: https://backtrace.io/docs/unity/
/// </summary>
public sealed class BacktraceConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("serverAddress")]
    public string ServerAddress { get; set; } = "";

    [JsonPropertyName("submissionToken")]
    public string SubmissionToken { get; set; } = "";

    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = new();

    [JsonPropertyName("database")]
    public DatabaseConfig Database { get; set; } = new();

    [JsonPropertyName("reporting")]
    public ReportingConfig Reporting { get; set; } = new();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServerAddress) && !string.IsNullOrWhiteSpace(SubmissionToken);
}

public sealed class DatabaseConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("path")]
    public string Path { get; set; } = "backtrace_database";

    [JsonPropertyName("maxRecords")]
    public int MaxRecords { get; set; } = 100;
}

public sealed class ReportingConfig
{
    [JsonPropertyName("unhandledExceptions")]
    public bool UnhandledExceptions { get; set; } = true;

    [JsonPropertyName("logErrors")]
    public bool LogErrors { get; set; } = true;

    [JsonPropertyName("handledExceptions")]
    public bool HandledExceptions { get; set; } = true;
}