namespace BattleLuck.Services.Planning;

using BattleLuck.Models;

public interface IPlanningStrategy
{
    PlanningStrategyType StrategyType { get; }
    Task<OperationResult<DeveloperPlan>> GeneratePlanAsync(PlanningRequest request);
}