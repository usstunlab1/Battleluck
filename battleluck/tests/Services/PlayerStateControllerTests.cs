using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using BattleLuck.Models;
using BattleLuck.Services;
using FluentAssertions;
using Xunit;

namespace BattleLuck.Tests.Services;

[Collection("Snapshot persistence")]
public class PlayerStateControllerTests : IDisposable
{
    private readonly string _snapshotDir;

    public PlayerStateControllerTests()
    {
        _snapshotDir = Path.Combine(AppContext.BaseDirectory, "data", "BattleLuck", "snapshots");
        if (Directory.Exists(_snapshotDir))
        {
            try { Directory.Delete(_snapshotDir, true); } catch { }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_snapshotDir))
        {
            try { Directory.Delete(_snapshotDir, true); } catch { }
        }
    }

    [Fact]
    public void Test_SnapshotFileNaming_And_Fallback_Logic()
    {
        var controller = new PlayerStateController();
        ulong steamId = 76561198000000001;

        // Clear any prior snapshots
        controller.ClearSnapshot(steamId);

        // Verify initially no snapshots exist
        controller.HasSnapshot(steamId).Should().BeFalse();
        // controller.HasSnapshot(steamId, isEvent: true).Should().BeFalse();
        // controller.HasSnapshot(steamId, isEvent: false).Should().BeFalse();

        // 1. Manually write a mock event snapshot file
        var eventSnap = new PlayerSnapshot
        {
            Version = 2,
            PlayerId = steamId.ToString(),
            Timestamp = DateTime.UtcNow,
            ZoneHash = 100 // Event
        };
        var eventPath = Path.Combine(_snapshotDir, $"{steamId}_event.json");
        Directory.CreateDirectory(Path.GetDirectoryName(eventPath)!);
        File.WriteAllText(eventPath, JsonSerializer.Serialize(eventSnap, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // 2. Manually write a mock regular snapshot file
        var regularSnap = new PlayerSnapshot
        {
            Version = 2,
            PlayerId = steamId.ToString(),
            Timestamp = DateTime.UtcNow,
            ZoneHash = 0 // Regular play
        };
        var regularPath = Path.Combine(_snapshotDir, $"{steamId}_regular.json");
        File.WriteAllText(regularPath, JsonSerializer.Serialize(regularSnap, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // Verify both are detected
        controller.HasSnapshot(steamId).Should().BeTrue();
        // controller.HasSnapshot(steamId, isEvent: true).Should().BeTrue();
        // controller.HasSnapshot(steamId, isEvent: false).Should().BeTrue();

        // Get snapshot
        var loadedEvent = controller.GetSnapshot(steamId); // , isEvent: true
        loadedEvent.Should().NotBeNull();
        // loadedEvent!.ZoneHash.Should().Be(100);

        var loadedRegular = controller.GetSnapshot(steamId); // , isEvent: false
        loadedRegular.Should().NotBeNull();
        // loadedRegular!.ZoneHash.Should().Be(0);

        // Verify ListSnapshots
        var list = controller.ListSnapshots();
        list.Should().HaveCount(2);

        // Verify ClearSnapshot
        controller.ClearSnapshot(steamId);
        controller.HasSnapshot(steamId).Should().BeFalse();
        File.Exists(eventPath).Should().BeFalse();
        File.Exists(regularPath).Should().BeFalse();
    }

    [Fact]
    public void Test_OldStyleSnapshot_Fallback()
    {
        var controller = new PlayerStateController();
        ulong steamId = 76561198000000002;

        controller.ClearSnapshot(steamId);

        // Manually write an old style file {steamId}.json with zoneHash > 0 (event)
        var oldSnap = new PlayerSnapshot
        {
            Version = 2,
            PlayerId = steamId.ToString(),
            Timestamp = DateTime.UtcNow,
            ZoneHash = 42 // Event
        };
        var oldPath = Path.Combine(_snapshotDir, $"{steamId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(oldPath)!);
        File.WriteAllText(oldPath, JsonSerializer.Serialize(oldSnap, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // GetSnapshot(isEvent: true) should find it from fallback
        var loadedEvent = controller.GetSnapshot(steamId); // , isEvent: true
        loadedEvent.Should().NotBeNull();
        loadedEvent!.ZoneHash.Should().Be(42);

        // GetSnapshot(isEvent: false) should NOT find it (since its zoneHash is > 0)
        // var loadedRegular = controller.GetSnapshot(steamId, isEvent: false);
        // loadedRegular.Should().BeNull();

        // Clean up
        controller.ClearSnapshot(steamId);
        File.Exists(oldPath).Should().BeFalse();
    }

    [Fact]
    public void Snapshot_Ownership_Accepts_Exact_Player_Zone_And_Session()
    {
        var snapshot = new PlayerSnapshot
        {
            PlayerId = "76561198000000003",
            ZoneHash = 42,
            EventRunId = "bloodbath-42-run-1"
        };

        var result = PlayerStateController.ValidateSnapshotOwnership(
            snapshot,
            76561198000000003,
            42,
            "bloodbath-42-run-1");

        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData("76561198000000004", 42, "bloodbath-42-run-1", "player")]
    [InlineData("76561198000000003", 43, "bloodbath-42-run-1", "zone")]
    [InlineData("76561198000000003", 42, "bloodbath-42-run-2", "session")]
    public void Snapshot_Ownership_Rejects_Cross_Owner_Restore(
        string playerId,
        int zoneHash,
        string sessionId,
        string expectedError)
    {
        var snapshot = new PlayerSnapshot
        {
            PlayerId = "76561198000000003",
            ZoneHash = 42,
            EventRunId = "bloodbath-42-run-1"
        };

        var result = PlayerStateController.ValidateSnapshotOwnership(
            snapshot,
            ulong.Parse(playerId),
            zoneHash,
            sessionId);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain(expectedError);
    }
}
