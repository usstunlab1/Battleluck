using System.Text.Json;
using BattleLuck.Models;
using BattleLuck.Services;
using FluentAssertions;
using Xunit;

namespace BattleLuck.Tests.Services;

public class SnapshotPersistenceTests : IDisposable
{
    private readonly string _snapshotDir;

    public SnapshotPersistenceTests()
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
    public void Snapshot_Files_Are_Created_And_Listed()
    {
        ulong steamId = 76561198000011111;

        SnapshotPersistence.Exists(steamId, true).Should().BeFalse();
        SnapshotPersistence.Exists(steamId, false).Should().BeFalse();

        var ev = new PlayerSnapshot { Version = 2, PlayerId = steamId.ToString(), Timestamp = DateTime.UtcNow, ZoneHash = 99 };
        var reg = new PlayerSnapshot { Version = 2, PlayerId = steamId.ToString(), Timestamp = DateTime.UtcNow, ZoneHash = 0 };

        SnapshotPersistence.Write(steamId, ev, true);
        SnapshotPersistence.Write(steamId, reg, false);

        SnapshotPersistence.Exists(steamId, true).Should().BeTrue();
        SnapshotPersistence.Exists(steamId, false).Should().BeTrue();

        var loadedEv = SnapshotPersistence.Read(steamId, true);
        var loadedReg = SnapshotPersistence.Read(steamId, false);
        loadedEv!.ZoneHash.Should().Be(99);
        loadedReg!.ZoneHash.Should().Be(0);

        var list = SnapshotPersistence.ListAll();
        list.Should().HaveCount(2);

        SnapshotPersistence.Delete(steamId, true);
        SnapshotPersistence.Delete(steamId, false);
        SnapshotPersistence.Exists(steamId, true).Should().BeFalse();
        SnapshotPersistence.Exists(steamId, false).Should().BeFalse();
    }

    [Fact]
    public void Legacy_File_Fallback_Respected_By_Category()
    {
        ulong steamId = 76561198000022222;
        Directory.CreateDirectory(_snapshotDir);
        var legacyPath = Path.Combine(_snapshotDir, $"{steamId}.json");
        var legacyEvent = new PlayerSnapshot
        {
            Version = 2,
            PlayerId = steamId.ToString(),
            Timestamp = DateTime.UtcNow,
            ZoneHash = 7
        };
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacyEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        SnapshotPersistence.Read(steamId, true)!.ZoneHash.Should().Be(7); // event finds it
        SnapshotPersistence.Read(steamId, false).Should().BeNull(); // regular does not

        // Deleting with matching category should also remove legacy file
        SnapshotPersistence.Delete(steamId, true);
        File.Exists(legacyPath).Should().BeFalse();
    }
}
