using BattleLuck.Services.Runtime;
namespace BattleLuck.Tests.Services;

public sealed class BuildAssetCatalogServiceTests
{
    [Theory]
    [InlineData(0f, true)]
    [InlineData(90f, true)]
    [InlineData(-90f, true)]
    [InlineData(270f, true)]
    [InlineData(45f, false)]
    public void Native_grid_rotations_are_limited_to_quarter_turns(float rotation, bool expected)
    {
        Assert.Equal(expected, BuildAssetCatalogService.IsQuarterTurn(rotation));
    }

    [Theory]
    [InlineData("Chain_Container_WorldChest_Iron_01", true)]
    [InlineData("TM_Chest_Standard", true)]
    [InlineData("Item_Chest_T08", false)]
    public void ChestClassifier_AllowsOnlyPhysicalContainerAssets(string prefab, bool expected)
    {
        Assert.Equal(expected, BuildAssetCatalogService.IsContainerChestName(prefab));
    }
}
