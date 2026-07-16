using System.Collections.Concurrent;

namespace BattleLuck.Services.AI;

/// <summary>A planner output retained for one day so admins can review it later.</summary>
public sealed class AiTaskRecord
{
    public string TaskId { get; init; } = "";
    public ulong OwnerSteamId { get; init; }
    public string Goal { get; init; } = "";
    public string Status { get; set; } = "planned";
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<AiActionPlanStep> Steps { get; init; } = new();
    public string RawPlannerOutput { get; init; } = "";
}

/// <summary>
/// In-memory AI planning task registry. Tasks are proposals only: execution still
/// requires the existing catalog validation and admin approval pipeline.
/// </summary>
public sealed class AiTaskService
{
    public static AiTaskService Instance { get; } = new();

    readonly ConcurrentDictionary<string, AiTaskRecord> _tasks = new(StringComparer.OrdinalIgnoreCase);
    readonly GameSystemsActionSource _source = new();

    public async Task<OperationResult<AiTaskRecord>> CreatePlanAsync(ulong ownerSteamId, string goal)
    {
        Prune();
        if (string.IsNullOrWhiteSpace(goal))
            return OperationResult<AiTaskRecord>.Fail("A task goal is required.");

        var assistant = BattleLuckPlugin.AIAssistant;
        if (assistant == null || !assistant.IsEnabled)
            return OperationResult<AiTaskRecord>.Fail("AI Assistant is not initialized. Configure ai_config.json and run .ai.reload.");

        var planner = new AiActionPlanner(_source);
        var plan = await planner.GeneratePlanAsync(
            goal.Trim(),
            ConversationStore.Instance.FormatForContext(8)).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var record = new AiTaskRecord
        {
            TaskId = $"task_{now:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..25],
            OwnerSteamId = ownerSteamId,
            Goal = goal.Trim(),
            Status = plan.Steps.Count > 0 ? "planned" : "needs_provider_or_review",
            CreatedUtc = now,
            UpdatedUtc = now,
            Steps = plan.Steps
                .Take(32)
                .Select(step => new AiActionPlanStep
                {
                    Action = step.Action,
                    Reason = step.Reason,
                    Confidence = Math.Clamp(step.Confidence, 0f, 1f)
                })
                .ToList(),
            RawPlannerOutput = Trim(plan.Raw, 4000)
        };

        _tasks[record.TaskId] = record;
        return OperationResult<AiTaskRecord>.Ok(record);
    }

    public IReadOnlyList<AiTaskRecord> List(ulong ownerSteamId, bool includeAll, int limit = 20)
    {
        Prune();
        return _tasks.Values
            .Where(task => includeAll || task.OwnerSteamId == ownerSteamId)
            .OrderByDescending(task => task.UpdatedUtc)
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();
    }

    public void Tick() => Prune();

    void Prune()
    {
        var cutoff = DateTime.UtcNow - ConversationStore.HistoryRetention;
        foreach (var pair in _tasks)
        {
            if (pair.Value.UpdatedUtc < cutoff)
                _tasks.TryRemove(pair.Key, out _);
        }
    }

    static string Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= max ? text : text[..Math.Max(1, max - 3)] + "...";
    }
}
