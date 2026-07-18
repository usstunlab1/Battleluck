using System.Collections.Concurrent;
using System.Reflection;
using BattleLuck.Services.AI;
using FluentAssertions;
using Xunit;

namespace BattleLuck.Tests.Services.AI;

public class AiTaskServiceTests
{
    [Fact]
    public void Prune_RemovesTasksOlderThanRetention()
    {
        var svc = AiTaskService.Instance;

        var tasksField = typeof(AiTaskService).GetField("_tasks", BindingFlags.NonPublic | BindingFlags.Instance);
        tasksField.Should().NotBeNull();
        var dict = (ConcurrentDictionary<string, AiTaskRecord>)tasksField!.GetValue(svc)!;
        dict.Clear();

        var now = DateTime.UtcNow;
        var old = new AiTaskRecord
        {
            TaskId = "old_task",
            OwnerSteamId = 1,
            Goal = "old",
            UpdatedUtc = now - ConversationStore.HistoryRetention - TimeSpan.FromMinutes(5)
        };
        var recent = new AiTaskRecord
        {
            TaskId = "recent_task",
            OwnerSteamId = 1,
            Goal = "recent",
            UpdatedUtc = now
        };

        dict[old.TaskId] = old;
        dict[recent.TaskId] = recent;

        // Act
        svc.Tick();

        dict.ContainsKey("recent_task").Should().BeTrue();
        dict.ContainsKey("old_task").Should().BeFalse();
    }

    [Fact]
    public void Trim_NormalizesWhitespace_And_EnforcesMax()
    {
        var mi = typeof(AiTaskService).GetMethod("Trim", BindingFlags.NonPublic | BindingFlags.Static);
        mi.Should().NotBeNull();

        var text = "  Hello\r\nworld!  ";
        var result = (string)mi!.Invoke(null, new object[] { text, 100 })!;
        // Trim() replaces newlines with spaces but does not collapse double spaces
        result.Should().Be("Hello  world!");

        var longText = new string('x', 2000);
        var limited = (string)mi!.Invoke(null, new object[] { longText, 10 })!;
        limited.Should().EndWith("...");
        limited.Length.Should().Be(10);
    }
}
