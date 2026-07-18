using System.Text.Json;
using BattleLuck.Models;
using BattleLuck.Services.Runtime;

namespace BattleLuck.Core.Loaders;

/// <summary>
/// Centralized loader for ModeConfig.
/// Supports both unified event definitions and legacy per-mode configurations.
/// </summary>
public static class ModeConfigLoader
{
    private static readonly EventDefinitionLoader _eventLoader = new();

    public static string EventsRoot => Path.Combine(ConfigLoader.ConfigRoot, "events");
    public static string KitsRoot => EventsRoot;

    /// <summary>
    /// Loads the effective ModeConfig for a given modeId.
    /// Prioritizes unified definitions but falls back to legacy if necessary.
    /// </summary>
    public static ModeConfig Load(string modeId)
    {
        // Try unified loader first
        try
        {
            return _eventLoader.LoadEffectiveConfig(modeId);
        }
        catch (FileNotFoundException)
        {
            // Fallback to legacy or basic config if unified not found
            return LoadLegacy(modeId);
        }
    }

    private static ModeConfig LoadLegacy(string modeId)
    {
        var config = new ModeConfig
        {
            ModeId = modeId,
            DisplayName = modeId,
            KitId = modeId
        };

        var legacyPath = Path.Combine(ConfigLoader.ConfigRoot, modeId);
        if (Directory.Exists(legacyPath))
        {
            // Load zones.json if exists
            var zonesPath = Path.Combine(legacyPath, "zones.json");
            if (File.Exists(zonesPath))
            {
                try
                {
                    config.Zones = JsonSerializer.Deserialize<ZonesConfig>(File.ReadAllText(zonesPath), ConfigLoader.JsonOptions) ?? new();
                }
                catch (Exception ex)
                {
                    BattleLuckPlugin.LogWarning($"[ModeConfigLoader] Failed to load legacy zones for '{modeId}': {ex.Message}");
                }
            }
        }

        // Load kit
        config.KitConfig = KitController.LoadKit(modeId) ?? new KitConfig();

        return config;
    }

    /// <summary>
    /// Ensure the file watcher is set up for mode config changes.
    /// </summary>
    public static void EnsureWatcher()
    {
        // No-op: ConfigLoader.EnsureDefaultsDeployed handles initial deployment.
        // File watching is handled by individual services that need it.
    }
}