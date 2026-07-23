namespace BattleLuck.Services.Planning;

using BattleLuck.Models;

public enum PlanningStrategyType
{
    /// <summary>Rule-based NPC selection for combat drills.</summary>
    CombatDrill,
    /// <summary>Manifest-constrained action selection for developer tasks.</summary>
    DeveloperBridge,
    /// <summary>LLM-based creative planning for general AI tasks.</summary>
    LlmCreative
}

public sealed class PlanningRequest
{
    public string Goal { get; }
    public PlanningStrategyType Strategy { get; }
    public object Context { get; }

    public PlanningRequest(string goal, PlanningStrategyType strategy, object context)
    {
        Goal = goal;
        Strategy = strategy;
        Context = context;
    }

    public T GetContext<T>() where T : class
    {
        if (Context is T typedContext) return typedContext;
        throw new InvalidCastException($"Planning context is not of type {typeof(T).Name}.");
    }
}

public sealed class DeveloperBridgeContext
{
    public DeveloperManifest Manifest { get; init; } = new();
}

public sealed class CombatDrillContext
{
    public float AveragePlayerLevel { get; init; }
    public int PlayerCount { get; init; }
}