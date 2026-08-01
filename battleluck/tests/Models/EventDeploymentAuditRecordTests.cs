using System.Text.Json;
using BattleLuck.Models;
using FluentAssertions;
using Xunit;

namespace BattleLuck.Tests.Models;

public class EventDeploymentAuditRecordTests
{
    [Fact]
    public void CanSerializeAndDeserialize_EventDeploymentAuditRecord()
    {
        // Arrange
        var record = new EventDeploymentAuditRecord
        {
            Timestamp = new DateTime(2026, 7, 17, 21, 46, 0, DateTimeKind.Utc),
            Command = "deploy",
            EventId = "bloodbath-001",
            Gist = "https://gist.github.com/test",
            Files = new Dictionary<string, bool> { { "event.json", true } },
            FileHashes = new Dictionary<string, string> { { "event.json", new string('a', 64) } },
            Validation = new DeploymentValidation { Json = true, Schema = true, Ids = true },
            Server = new DeploymentServerStatus { RegisterOk = true, StartOk = true },
            Rollback = false,
            Exit = 0,
            RestoredPlayers = 5,
            SkippedPlayers = 0
        };

        var options = new JsonSerializerOptions { WriteIndented = true };

        // Act
        string json = JsonSerializer.Serialize(record, options);
        var deserialized = JsonSerializer.Deserialize<EventDeploymentAuditRecord>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Timestamp.Should().Be(record.Timestamp);
        deserialized.Command.Should().Be(record.Command);
        deserialized.EventId.Should().Be(record.EventId);
        deserialized.Gist.Should().Be(record.Gist);
        deserialized.Files.Should().HaveCount(1).And.ContainKey("event.json");
        deserialized.FileHashes.Should().HaveCount(1).And.ContainKey("event.json");
        deserialized.Validation.Json.Should().BeTrue();
        deserialized.Server.RegisterOk.Should().BeTrue();
        deserialized.Exit.Should().Be(0);
        deserialized.RestoredPlayers.Should().Be(5);
    }

    [Fact]
    public void Deserialization_HandlesMissingOptionalFields()
    {
        // Arrange
        string json = @"{
            ""timestamp"": ""2026-07-17T21:46:00Z"",
            ""command"": ""deploy"",
            ""eventId"": ""bloodbath-001"",
            ""files"": {},
            ""fileHashes"": {},
            ""validation"": { ""json"": true, ""schema"": true, ""ids"": true },
            ""server"": { ""registerOk"": true, ""startOk"": true },
            ""rollback"": false,
            ""exit"": 0
        }";

        // Act
        var record = JsonSerializer.Deserialize<EventDeploymentAuditRecord>(json);

        // Assert
        record.Should().NotBeNull();
        record!.Gist.Should().BeNull();
        record.ErrorCode.Should().BeNull();
        record.Error.Should().BeNull();
        record.Backup.Should().BeNull();
        record.RestoredPlayers.Should().Be(0);
        record.SkippedPlayers.Should().Be(0);
    }
}
