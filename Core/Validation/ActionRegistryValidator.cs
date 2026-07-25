using System.Text.Json;
using BattleLuck.Services.Runtime;

namespace BattleLuck.Core.Validation;

public class ActionRegistryValidator : IActionValidator
{
    private readonly HashSet<string> _allowedActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _initializationIssues = new();

    public ActionRegistryValidator()
    {
        var catalogPath = Path.Combine(ConfigLoader.ConfigRoot, "actions_catalog.json");
        if (!File.Exists(catalogPath))
        {
            _initializationIssues.Add("Action catalog 'actions_catalog.json' not found.");
            return;
        }

        try
        {
            using var stream = File.OpenRead(catalogPath);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("registered", out var registered) && registered.ValueKind == JsonValueKind.Array)
            {
                foreach (var action in registered.EnumerateArray())
                {
                    var value = action.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        _allowedActions.Add(value);
                }
            }
        }
        catch (Exception ex)
        {
            _initializationIssues.Add($"Action catalog parse failed: {ex.Message}");
        }
    }

    public IReadOnlyList<string> Validate(string modeId, ModeConfig config)
    {
        var issues = new List<string>();
        issues.AddRange(_initializationIssues);

        foreach (var flow in EnumerateModeFlows(config))
        {
            foreach (var flowDefinition in flow.FlowConfig.Flows)
            {
                foreach (var actionString in flowDefinition.Value.Actions)
                {
                    var actionName = ExtractActionName(actionString);
                    if (string.IsNullOrWhiteSpace(actionName))
                        continue;

                    if (!_allowedActions.Contains(actionName))
                        issues.Add($"Unknown action '{actionName}' in flow phase '{flow.PhaseName}/{flowDefinition.Key}'.");
                }
            }
        }

        return issues;
    }

    private static IEnumerable<(string PhaseName, FlowConfig FlowConfig)> EnumerateModeFlows(ModeConfig config)
    {
        yield return ("enter", config.FlowEnter);
        yield return ("start", config.Session.Flow.Start);
        yield return ("tracking", config.Session.Flow.Tracking);
        yield return ("winner", config.Session.Flow.Winner);
        yield return ("ending", config.Session.Flow.Ending);
        yield return ("exit", config.FlowExit);
    }

    private static string ExtractActionName(string actionString)
    {
        var action = (actionString ?? string.Empty).Trim();
        var separatorIndex = action.IndexOf(':');
        return separatorIndex >= 0 ? action[..separatorIndex].Trim() : action;
    }

    /// <summary>
    /// Normalizes action name using the runtime ActionRegistry alias mappings.
    /// </summary>
    public static string NormalizeActionName(string actionName)
    {
        return BattleLuck.Services.Runtime.ActionManifestService.Instance.Normalize(actionName);
    }

    /// <summary>
    /// Checks if an action is registered/known via the runtime ActionRegistry.
    /// </summary>
    public static bool IsKnown(string actionName)
    {
        return BattleLuck.Services.Runtime.ActionManifestService.Instance.IsKnown(actionName);
    }
}

public interface IActionValidator
{
    IReadOnlyList<string> Validate(string modeId, ModeConfig config);
}
