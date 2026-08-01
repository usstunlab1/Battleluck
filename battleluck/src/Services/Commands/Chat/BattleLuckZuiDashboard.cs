namespace BattleLuck.Services.Chat;

/// <summary>
/// Builds the compact, server-authored BattleLuck dashboard consumed by an
/// optional ZUI-capable V Rising client. The server remains authoritative:
/// buttons contain normal commands and never mutate client-side state directly.
/// </summary>
public static class BattleLuckZuiDashboard
{
    static readonly HashSet<string> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        "home", "events", "players", "world", "content", "loot", "buffs", "signs",
        "weapons", "actions", "ai", "audit"
    };

    public static bool IsKnownRoute(string? route) =>
        !string.IsNullOrWhiteSpace(route) && Routes.Contains(route.Trim());

    public static ZuiWindow BuildHome() => new(
        "battleluck.home",
        "BATTLELUCK // VIEW",
        new[]
        {
            "ONLINE · read only"
        },
        new[]
        {
            new ZuiButton("Events", ".ai ui events"),
            new ZuiButton("Players", ".ai ui players"),
            new ZuiButton("World", ".ai ui world"),
            new ZuiButton("Content", ".ai ui content"),
            new ZuiButton("Audit", ".ai ui audit")
        });

    public static ZuiWindow BuildSection(string section)
    {
        var normalized = (section ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "events" => Section("events", "EVENT VIEW", "Modes, waves, triggers, objectives"),
            "players" => Section("players", "PLAYER VIEW", "Sessions, teams, kits, rollback"),
            "world" => Section("world", "WORLD VIEW", "Zones, tracked objects, NPCs"),
            "content" => BuildContent(),
            "loot" => Section("loot", "LOOT VIEW", "Tables, crate rules, reward claims"),
            "buffs" => Section("buffs", "BUFF VIEW", "Native BattleLuck buff presets and active effects"),
            "signs" => Section("signs", "SIGN VIEW", "Tracked native sign presets and ownership"),
            "weapons" => Section("weapons", "WEAPON VIEW", "Native weapon search, kits, and grants"),
            "actions" => Section("actions", "ACTION VIEW", "Registered native actions and safety metadata"),
            "ai" => Section("ai", "AI VIEW", "Provider health, approvals, action planning"),
            "audit" => Section("audit", "AUDIT VIEW", "Release readiness and validation evidence"),
            _ => BuildHome()
        };
    }

    public static string BuildNativeText(string? section = null)
    {
        var window = string.IsNullOrWhiteSpace(section) ||
                     section.Equals("home", StringComparison.OrdinalIgnoreCase)
            ? BuildHome()
            : BuildSection(section);
        return $"{window.Title}\n{string.Join("\n", window.Lines)}\n" +
               $"Views: {string.Join(", ", Routes.Where(route => !route.Equals("home", StringComparison.OrdinalIgnoreCase)))}";
    }

    static ZuiWindow BuildContent() => new(
        "battleluck.content",
        "CONTENT VIEW",
        new[] { "Read-only catalogs" },
        new[]
        {
            new ZuiButton("Loot", ".ai ui loot"),
            new ZuiButton("Buffs", ".ai ui buffs"),
            new ZuiButton("Signs", ".ai ui signs"),
            new ZuiButton("Weapons", ".ai ui weapons"),
            new ZuiButton("Actions", ".ai ui actions"),
            new ZuiButton("AI", ".ai ui ai"),
            new ZuiButton("Back", ".ai ui")
        });

    static ZuiWindow Section(string id, string title, string summary) => new(
        $"battleluck.{id}",
        title,
        new[] { summary, "View only. No button can execute or confirm a mutation." },
        new[]
        {
            new ZuiButton("Refresh", $".ai ui {id}"),
            new ZuiButton("Back", ".ai ui")
        });
}
