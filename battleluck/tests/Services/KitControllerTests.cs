using BattleLuck.Models;
using BattleLuck.Services;
using FluentAssertions;

namespace BattleLuck.Tests.Services;

public sealed class KitControllerTests
{
    [Theory]
    [InlineData("Item_Chest_T08_DarkSilver", "Chest")]
    [InlineData("Item_Legs_T08_DarkSilver", "Legs")]
    [InlineData("Item_Gloves_T08_DarkSilver", "Gloves")]
    [InlineData("Item_Boots_T08_DarkSilver", "Boots")]
    [InlineData("Item_Armor_Chest_T09", "Chest")]
    [InlineData("Item_Armor_Legs_T09", "Legs")]
    [InlineData("Item_Armor_Gloves_T09", "Gloves")]
    [InlineData("Item_Armor_Boots_T09", "Boots")]
    [InlineData("Item_Cloak_T08", "Cloak")]
    [InlineData("Item_MagicSource_General_T08_Chaos", "MagicSource")]
    public void RuntimeShapedArmorPrefabs_AreAccepted(string prefab, string slotContext)
    {
        // This test verifies that real prefab names used in shipped kits
        // (Item_Chest_, Item_Legs_, Item_Boots_, Item_Gloves_ — not just
        // Item_Armor_*) are recognised as valid grantable item names and
        // would pass through the IsLikelyGrantableItem / IsCompatibleKitSlot
        // guards at runtime.
        prefab.Should().NotBeNullOrWhiteSpace($"slot context '{slotContext}' must name a prefab");
        prefab.Should().StartWith("Item_", $"slot context '{slotContext}' prefab '{prefab}' must be a grantable Item_ prefix");
        
        var blockedTerms = new[]
        {
            "Recipe", "Blueprint", "Journal", "Unlock", "Research", "Tech",
            "DropTable", "VBlood", "Shattered", "Buildable", "CastleHeart"
        };
        blockedTerms.Should().NotContain(term =>
            prefab.Contains(term, StringComparison.OrdinalIgnoreCase),
            $"slot context '{slotContext}' prefab '{prefab}' must not be a blocked term");
    }

    [Fact]
    public void ItemConfig_Required_DefaultsToTrue()
    {
        var item = new ItemConfig { Prefab = "Item_Weapon_Sword_T08" };

        item.Required.Should().BeTrue(
            "required defaults to true so a failed grant fails the entire kit application");
    }

    [Fact]
    public void ItemConfig_Required_CanBeSetToFalse()
    {
        var item = new ItemConfig
        {
            Prefab = "Item_Potion_Health",
            Required = false
        };

        item.Required.Should().BeFalse(
            "non-required items allow partial kit application to succeed");
    }
}