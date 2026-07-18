using BepInEx.Configuration;
using BattleLuck.Models;

/// <summary>
/// Small, server-owner friendly feature switches stored in BepInEx/config/gg.battleluck.cfg.
/// Detailed event definitions and provider credentials remain in config/BattleLuck/*.json.
/// </summary>
public static class PluginSettings
{
    static ConfigEntry<bool>? _eventsEnabled;
    static ConfigEntry<string>? _enabledEventModes;
    static ConfigEntry<bool>? _actionsEnabled;
    static ConfigEntry<bool>? _aiEnabled;
    static ConfigEntry<string>? _aiProvider;
    static ConfigEntry<bool>? _aiEventAuthoringEnabled;
    static ConfigEntry<bool>? _aiAutoExecuteNpcActions;

    public static bool EventsEnabled => _eventsEnabled?.Value ?? true;
    public static bool ActionsEnabled => _actionsEnabled?.Value ?? true;

    public static void Initialize(ConfigFile config)
    {
        _eventsEnabled = config.Bind("Events", "Enabled", true,
            "Enable declarative BattleLuck events and game modes.");
        _enabledEventModes = config.Bind("Events", "EnabledModes", "*",
            "Comma-separated event IDs to load, or * for every enabled event bundle.");

        _actionsEnabled = config.Bind("Actions", "Enabled", true,
            "Allow catalog-backed event, command, and AI actions to mutate game state.");

        _aiEnabled = config.Bind("AI", "Enabled", true,
            "Master switch for the BattleLuck AI assistant. ai_config.json must also enable AI.");
        _aiProvider = config.Bind("AI", "Provider", "config",
            "AI provider override: config, auto, local, llama, cloudflare, or google.");
        _aiEventAuthoringEnabled = config.Bind("AI", "EventAuthoringEnabled", true,
            "Allow approval-gated AI event creation and editing.");
        _aiAutoExecuteNpcActions = config.Bind("AI", "AutoExecuteNpcActions", false,
            "Allow ProjectM AI-group NPC actions to execute automatically. Keep false on production servers.");
    }

    public static bool IsEventModeEnabled(string modeId)
    {
        if (!EventsEnabled)
            return false;

        var configured = _enabledEventModes?.Value ?? "*";
        if (configured.Trim() == "*")
            return true;

        return configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(modeId, StringComparer.OrdinalIgnoreCase);
    }

    public static void ApplyAIOverrides(AIConfig config)
    {
        config.Enabled &= _aiEnabled?.Value ?? true;
        config.EventAuthoring.Enabled &= _aiEventAuthoringEnabled?.Value ?? true;
        config.ProjectMAiGroup.AutoExecute = _aiAutoExecuteNpcActions?.Value ?? false;

        var provider = (_aiProvider?.Value ?? "config").Trim();
        if (!provider.Equals("config", StringComparison.OrdinalIgnoreCase) && provider.Length > 0)
            config.Provider = provider;
    }
}
