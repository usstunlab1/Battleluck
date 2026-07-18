using System.Reflection;
using BattleLuck.Models;
using BattleLuck.Services.Runtime;
using Xunit;
using FluentAssertions;

namespace BattleLuck.Tests.Services;

public class RoadmapServiceTests
{
    private string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "BL_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteJson(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    [Fact]
    public void Snapshot_And_Context_FromSeededDefinition_AreCorrect()
    {
        var svc = new RoadmapService();

        var def = new RoadmapDefinition
        {
            Project = "BattleLuck",
            Description = "Test Roadmap",
            Roles = new List<RoadmapRole>
            {
                new() { Id = "llm", Title = "Assistant", Description = "LLM role", Capabilities = new(){"plan"}, Guardrails = new(){"no destructive"} }
            },
            Milestones = new List<RoadmapMilestone>
            {
                new() { Id = "m1", Title = "First", Status = "active", Summary = "Do first", Acceptance = new(){"A","B"} }
            }
        };

        var defField = typeof(RoadmapService).GetField("_definition", BindingFlags.NonPublic | BindingFlags.Instance)!;
        defField.SetValue(svc, def);
        var loadedField = typeof(RoadmapService).GetField("_loadedAtUtc", BindingFlags.NonPublic | BindingFlags.Instance)!;
        loadedField.SetValue(svc, DateTime.UtcNow);

        var snap = svc.GetSnapshot();
        snap.Project.Should().Be("BattleLuck");
        snap.Description.Should().Be("Test Roadmap");
        snap.Milestones.Should().HaveCount(1);
        snap.Roles.Should().HaveCount(1);

        var m1 = svc.FindMilestone("m1");
        m1.Should().NotBeNull();
        m1!.Status.Should().Be("active");
        m1.Acceptance.Should().Contain(new[] { "A", "B" });

        var context = svc.BuildPromptContext("llm", focusId: "m1");
        context.Should().Contain("SERVER ROADMAP (read-only context)");
        context.Should().Contain("Role: Assistant (llm)");
        context.Should().Contain("[active] m1: First (FOCUS)");
    }

    [Fact]
    public void SeededErrorState_ReportsError_And_EmptyCollections()
    {
        var svc = new RoadmapService();
        typeof(RoadmapService).GetField("_lastError", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(svc, "Missing roadmap config");
        typeof(RoadmapService).GetField("_loadedAtUtc", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(svc, DateTime.UtcNow);

        svc.IsLoaded.Should().BeTrue();
        svc.LastError.Should().NotBeNull();
        var snap = svc.GetSnapshot();
        snap.Milestones.Should().BeEmpty();
        snap.Roles.Should().BeEmpty();
    }
}
