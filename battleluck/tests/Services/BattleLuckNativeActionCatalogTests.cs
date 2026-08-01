using BattleLuck.Models;
using BattleLuck.Services.Runtime;

namespace BattleLuck.Tests.Services;

public sealed class BattleLuckNativeActionCatalogTests
{
    [Fact]
    public void Catalog_is_owned_by_BattleLuck_and_has_no_external_mod_contract()
    {
        Assert.Equal(27, BattleLuckNativeActionCatalog.Entries.Count);

        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Services", "Infrastructure", "BattleLuckNativeActionCatalog.cs"));

        Assert.DoesNotContain("Kindred", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Scarlet", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VRisingBuild", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Injected_entries_enforce_server_only_contract_and_mutation_approval()
    {
        var entries = new Dictionary<string, ActionManifestEntry>(StringComparer.OrdinalIgnoreCase);

        BattleLuckNativeActionCatalog.InjectEntries(entries);

        Assert.Equal(BattleLuckNativeActionCatalog.Entries.Count, entries.Count);
        Assert.All(entries.Values, entry =>
        {
            Assert.True(entry.HandlerAvailable);
            Assert.True(entry.Executable);
            Assert.True(entry.IsServerContractValid);
            Assert.True(entry.MainThreadRequired);

            if (entry.IsMutating)
            {
                Assert.True(entry.RequiresApproval);
                Assert.False(entry.AllowedPhases.HasFlag(SessionPhaseAllowance.NoSession));
                Assert.False(entry.IsSourcePermitted(ActionSource.Player));
            }
            else
            {
                Assert.False(entry.RequiresApproval);
                Assert.Equal("safe", entry.RiskLevel);
                Assert.True(entry.IsSourcePermitted(ActionSource.Player));
            }
        });
    }

    [Theory]
    [InlineData("Item_Weapon_Sword_T08_Sanguine", true)]
    [InlineData("Item_Consumable_GlassBottle_BloodRosePotion_T01", true)]
    [InlineData("Buff_General_Ignite", false)]
    [InlineData("Chain_Container_WorldChest_Iron_01", false)]
    [InlineData("", false)]
    public void Inventory_loot_classification_is_explicit(string prefab, bool expected)
    {
        Assert.Equal(expected, BattleLuckNativeActionCatalog.IsInventoryLootPrefab(prefab));
    }

    [Fact]
    public void Additive_grants_require_a_stable_reward_id()
    {
        Assert.Contains("rewardId", BattleLuckNativeActionCatalog.Entries["loot.grant"].Required);
        Assert.Contains("rewardId", BattleLuckNativeActionCatalog.Entries["loot.roll"].Required);
        Assert.Contains("rewardId", BattleLuckNativeActionCatalog.Entries["loot.kit.grant"].Required);
        Assert.Contains("rewardId", BattleLuckNativeActionCatalog.Entries["weapon.grant"].Required);
        Assert.Contains("rewardId", BattleLuckNativeActionCatalog.Entries["weapon.kit.grant"].Required);
    }

    [Fact]
    public void Buff_rewards_are_limited_to_presets_or_the_active_zone_table()
    {
        Assert.True(BattleLuckNativeActionCatalog.IsAllowedBuffReward("Buff_General_Ignite", null));
        Assert.False(BattleLuckNativeActionCatalog.IsAllowedBuffReward("Buff_Arbitrary_AdminPower", null));

        var zone = new ZoneDefinition
        {
            LootCrates = new LootCrateConfig
            {
                CrateTypes =
                {
                    new CrateTypeConfig { Prefab = "Buff_Cloak_Mounted", Weight = 1 }
                }
            }
        };
        Assert.True(BattleLuckNativeActionCatalog.IsAllowedBuffReward("Buff_Cloak_Mounted", zone));
        Assert.False(BattleLuckNativeActionCatalog.IsAllowedBuffReward("Buff_Arbitrary_AdminPower", zone));
    }

    [Theory]
    [InlineData("actions.search")]
    [InlineData("loot.roll")]
    [InlineData("buff.preset.apply")]
    [InlineData("sign.spawn")]
    [InlineData("weapon.grant")]
    [InlineData("zone.marker.spawn")]
    public void Requested_action_families_have_real_native_handlers(string action)
    {
        Assert.True(BattleLuckNativeActionCatalog.IsNativeAction(action));
        Assert.True(BattleLuckNativeActionCatalog.Entries.ContainsKey(action));
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
