using System.Text.Json;
using System.Text.RegularExpressions;
using BattleLuck.Services.Flow;
using BattleLuck.Services.Runtime;

namespace BattleLuck.Tests.Services;

public sealed class ActionCatalogCompletenessTests
{
    [Fact]
    public void Catalog_documentation_and_handler_boundary_are_complete()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "config", "BattleLuck", "actions_catalog.json")));

        var registered = document.RootElement.GetProperty("registered")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var metadata = document.RootElement.GetProperty("metadata")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(registered);
        Assert.True(registered.SetEquals(metadata), "Every registered action must have exactly one metadata record.");

        var executorSource = File.ReadAllText(Path.Combine(
            root, "src", "Services", "Events", "Flow", "FlowActionExecutor.cs"));
        var switchCases = Regex.Matches(executorSource, "case\\s+\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .Where(registered.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var manifestSource = File.ReadAllText(Path.Combine(
            root, "src", "Services", "Validation", "ActionManifestService.cs"));
        var catalogOnlyStart = manifestSource.IndexOf(
            "public static readonly IReadOnlySet<string> CatalogOnlyActions",
            StringComparison.Ordinal);
        Assert.True(catalogOnlyStart >= 0);
        var catalogOnlyEnd = manifestSource.IndexOf("};", catalogOnlyStart, StringComparison.Ordinal);
        Assert.True(catalogOnlyEnd > catalogOnlyStart);
        var catalogOnlySource = manifestSource[catalogOnlyStart..catalogOnlyEnd];
        var catalogOnlyActions = Regex.Matches(catalogOnlySource, "\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expectedCatalogOnly = registered.Except(switchCases, StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(
            expectedCatalogOnly.SetEquals(catalogOnlyActions),
            "Catalog-only actions must exactly equal registered names without an executor switch case.");

        Assert.NotEmpty(switchCases);
        Assert.NotEmpty(catalogOnlyActions);
        Assert.NotEmpty(RuntimeEffectActionCatalog.Entries);
        Assert.NotEmpty(BattleLuckNativeActionCatalog.Entries);
        Assert.Empty(registered.Intersect(RuntimeEffectActionCatalog.Entries.Keys, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(registered.Intersect(BattleLuckNativeActionCatalog.Entries.Keys, StringComparer.OrdinalIgnoreCase));
        Assert.Empty(RuntimeEffectActionCatalog.Entries.Keys.Intersect(
            BattleLuckNativeActionCatalog.Entries.Keys,
            StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Legacy_alias_targets_and_high_risk_approval_policy_are_complete()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "config", "BattleLuck", "actions_catalog.json")));
        var root = document.RootElement;
        var registered = root.GetProperty("registered")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in root.GetProperty("llm_guidance").GetProperty("legacy_mappings").EnumerateObject())
        {
            Assert.Contains(mapping.Value.GetString()!, registered);
            Assert.False(string.IsNullOrWhiteSpace(mapping.Name));
        }

        foreach (var metadata in root.GetProperty("metadata").EnumerateObject())
        {
            var entry = metadata.Value;
            var risk = entry.GetProperty("riskLevel").GetString();
            var requiresApproval = entry.GetProperty("requiresApproval").GetBoolean();
            if (risk is "high" or "critical" or "destructive")
                Assert.True(requiresApproval, $"{metadata.Name} ({risk}) must require approval.");
        }
    }

    [Fact]
    public void Catalog_only_entries_are_not_exposed_as_executable_handlers()
    {
        var json = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "config", "BattleLuck", "actions_catalog.json"));
        var manifest = Assert.IsType<ActionManifestService>(ActionManifestService.FromJson(json));

        Assert.All(ActionManifestService.CatalogOnlyActions, action =>
        {
            Assert.True(manifest.TryGetAction(action, out var entry), action);
            Assert.NotNull(entry);
            Assert.False(entry!.HandlerAvailable, action);
            Assert.False(entry.Executable, action);
            Assert.False(FlowActionExecutor.IsRuntimeHandledAction(action), action);
        });
    }

    [Fact]
    public void Retired_glow_actions_and_global_free_build_are_not_executable()
    {
        Assert.Contains("glow.enable", ActionManifestService.CatalogOnlyActions);
        Assert.Contains("glow.disable", ActionManifestService.CatalogOnlyActions);
        Assert.False(FlowActionExecutor.IsRuntimeHandledAction("glow.enable"));
        Assert.False(FlowActionExecutor.IsRuntimeHandledAction("glow.disable"));

        var executorSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Services", "Events", "Flow", "FlowActionExecutor.cs"));
        Assert.Contains("build.free is permanently disabled", executorSource);
        Assert.DoesNotContain("after build.free", executorSource);
        Assert.DoesNotContain("BuildingRestrictionController.DisableRestrictions", executorSource);
        Assert.DoesNotContain("BuildingRestrictionController.EnableRestrictions", executorSource);
    }

    [Fact]
    public void Chest_spawn_uses_the_shared_asset_catalog_inside_an_active_zone()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "config", "BattleLuck", "actions_catalog.json")));
        var chest = document.RootElement
            .GetProperty("metadata")
            .GetProperty("chest.spawn");

        var required = chest.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        var optional = chest.GetProperty("optional")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        Assert.Contains("position", required);
        Assert.Contains("prefab", optional);
        Assert.Contains("prefabGuid", optional);
        Assert.Equal(
            "Chain_Container_WorldChest_Iron_01",
            chest.GetProperty("defaults").GetProperty("prefab").GetString());

        var executorSource = File.ReadAllText(Path.Combine(
            root, "src", "Services", "Events", "Flow", "FlowActionExecutor.cs"));
        Assert.Contains("BuildAssetCatalogService.ResolveForBlueprint(prefab, prefabGuid, \"chest\")", executorSource);
        Assert.Contains("Chain_Container_WorldChest_Iron_01", executorSource);
        Assert.Contains("chest.spawn requires an active BattleLuck session zone", executorSource);
        Assert.Contains("\"blueprint_chest\"", executorSource);
        Assert.Contains("var trackingGroup = c.GameContext.SessionId", executorSource);
    }

    static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BattleLuck.csproj")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the BattleLuck repository root.");
    }
}
