namespace BattleLuck.Services.Planning;

using BattleLuck.Models;

public sealed class UnifiedPlannerService
{
    private readonly Dictionary<PlanningStrategyType, IPlanningStrategy> _strategies;

    public UnifiedPlannerService(IEnumerable<IPlanningStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.StrategyType);
    }

    public Task<OperationResult<DeveloperPlan>> GeneratePlanAsync(PlanningRequest request)
    {
        if (_strategies.TryGetValue(request.Strategy, out var strategy))
        {
            return strategy.GeneratePlanAsync(request);
        }

        return Task.FromResult(OperationResult<DeveloperPlan>.Fail($"Planning strategy '{request.Strategy}' not found."));
    }
}