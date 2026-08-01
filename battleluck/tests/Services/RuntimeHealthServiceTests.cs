using BattleLuck.Services.Runtime;

namespace BattleLuck.Tests.Services;

public sealed class RuntimeHealthServiceTests
{
    [Fact]
    public void Snapshot_is_bounded_versioned_and_expires()
    {
        var now = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var snapshot = RuntimeHealthService.CreateSnapshot(" READY ", true, true, now);

        Assert.Equal("battleluck.health", snapshot.ContractName);
        Assert.Equal("1.0.0", snapshot.SchemaVersion);
        Assert.Equal("ready", snapshot.Status);
        Assert.True(snapshot.Initialized);
        Assert.True(snapshot.WorldReady);
        Assert.Equal(now, snapshot.GeneratedAtUtc);
        Assert.Equal(now.AddSeconds(15), snapshot.ExpiresAtUtc);
    }
}
