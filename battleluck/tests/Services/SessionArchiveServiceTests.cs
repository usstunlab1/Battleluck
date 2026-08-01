using BattleLuck.Models;
using BattleLuck.Services.Runtime;
using BattleLuck.Utilities;

namespace BattleLuck.Tests.Services;

public sealed class SessionArchiveServiceTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "BattleLuckTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Archive_WritesNonSensitiveTerminalRecord_AndListsIt()
    {
        var service = new SessionArchiveService(_root);
        var result = service.Archive(new SessionArchiveRecord
        {
            SessionId = "run_123",
            ModeId = "bloodbath",
            ZoneHash = 100,
            Reason = "rollback",
            CreatedUtc = DateTime.UtcNow.AddMinutes(-2),
            WasStarted = true,
            ParticipantCount = 3
        });

        Assert.True(result.Success, result.Error);
        var record = Assert.IsType<SessionArchiveRecord>(result.Value);
        Assert.True(SafeFileSystem.IsSafeIdentifier(record.ArchiveId));
        Assert.Equal("rollback", record.Reason);

        var listed = Assert.Single(service.List());
        Assert.Equal(record.ArchiveId, listed.ArchiveId);
        Assert.Equal("bloodbath", listed.ModeId);
        Assert.Equal(3, listed.ParticipantCount);
    }

    [Fact]
    public void Delete_RequiresExactConfirmationAndCannotEscapeArchiveRoot()
    {
        var service = new SessionArchiveService(_root);
        var archived = service.Archive(new SessionArchiveRecord { SessionId = "run", ModeId = "mode", Reason = "destroy" });
        var record = Assert.IsType<SessionArchiveRecord>(archived.Value);

        Assert.False(service.Delete(record.ArchiveId, confirmed: false).Success);
        Assert.False(service.Delete("..", confirmed: true).Success);
        Assert.True(service.Delete(record.ArchiveId, confirmed: true).Success);
        Assert.Empty(service.List());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
