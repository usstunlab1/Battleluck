// ── Mode configuration aggregate ────────────────────────────────────────
// Extracted from Core/ConfigLoader.cs (Stage A) so future phases have a
// stable, discoverable model target. Declared in the global namespace to
// match the rest of the config model graph.

/// <summary>
/// Aggregate configuration for a single game mode, composed from the
/// per-file configs under config/BattleLuck/&lt;modeId&gt;/.
/// </summary>
public sealed class ModeConfig
{
    /// <summary>
    /// Event definition identifier. Historically named ModeId for compatibility.
    /// </summary>
    public string ModeId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public int Version { get; set; } = 1;
    public string KitId { get; set; } = "";

    /// <summary>
    /// Unified event definitions use SessionController's built-in snapshot,
    /// kit, team, teleport, death, and restore lifecycle when explicit legacy
    /// enter/exit flows are absent.
    /// </summary>
    public bool UsesManagedPlayerLifecycle { get; set; }

    public SessionConfig Session { get; set; } = new();
    public RulesConfig Rules { get; set; } = new();
    public ZonesConfig Zones { get; set; } = new();
    public KitConfig KitConfig { get; set; } = new();
    public ManifestConfig Manifest { get; set; } = new();
    public FlowConfig FlowEnter { get; set; } = new();
    public FlowConfig FlowExit { get; set; } = new();

    public KitConfig? Kit { get; set; }
    public WallBoundaryConfig? Border { get; set; }
    public PrefabConfig[] Prefabs { get; set; } = Array.Empty<PrefabConfig>();
    public BattleLuck.Models.SchematicConfig[] Schematics { get; set; } = Array.Empty<BattleLuck.Models.SchematicConfig>();

    /// <summary>
    /// Parsed frontmatter from an Events/&lt;modeId&gt;/prompt.txt, when present.
    /// DEPRECATED: This property is kept for legacy compatibility. The canonical
    /// AI policy is now in event.json ai block. prompt.txt is optional and
    /// contains only narrative override text.
    /// </summary>
    public EventPromptDefinition? EventPrompt { get; set; }
}
