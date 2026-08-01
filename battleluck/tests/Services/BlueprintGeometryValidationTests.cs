using BattleLuck.Models;
using BattleLuck.Services.Geometry;

namespace BattleLuck.Tests.Services;

public sealed class BlueprintGeometryValidationTests
{
    [Fact]
    public void Tile_geometry_requires_a_native_quarter_turn()
    {
        var placement = Tile("wall-a", rotation: 45f);

        Assert.False(BlueprintGeometryService.ValidatePlacement(placement, radius: 0f, margin: 1f).Success);

        placement.Rotation = 90f;
        Assert.True(BlueprintGeometryService.ValidatePlacement(placement, radius: 0f, margin: 1f).Success);
    }

    [Theory]
    [InlineData("Chain_Container_WorldChest_Iron_01", true)]
    [InlineData("TM_Chest_01", true)]
    [InlineData("Item_Weapon_Sword_T08_Sanguine", false)]
    public void Chest_geometry_is_explicitly_allowlisted(string prefab, bool expected)
    {
        var placement = new BlueprintGeometryPlacement
        {
            Id = "chest-a",
            Prefab = prefab,
            Kind = "chest",
            Offset = new Vec3Config()
        };

        Assert.Equal(expected, BlueprintGeometryService.ValidatePlacement(placement, radius: 0f, margin: 1f).Success);
    }

    static BlueprintGeometryPlacement Tile(string id, float rotation) => new()
    {
        Id = id,
        Prefab = "TM_Wall_Test",
        Kind = "wall",
        Rotation = rotation,
        Offset = new Vec3Config()
    };
}
