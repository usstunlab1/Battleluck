using BattleLuck.Utilities;
using FluentAssertions;

namespace BattleLuck.Tests.Utilities;

public class PrefabCatalogTests
{
    [Fact]
    public void Archive_LoadsEveryNamedPrefab()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var catalog = PrefabCatalog.Load(Path.Combine(root, "Data", "render-prefabs.json"));

        catalog.Count.Should().BeGreaterThan(5000);
    }

    [Fact]
    public void ExportActions_ContainsEveryArchivedPrefab()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var catalog = PrefabCatalog.Load(Path.Combine(root, "Data", "render-prefabs.json"));

        var actions = catalog.ExportActions().ToList();

        actions.Should().HaveCount(catalog.Count);
        actions.Should().Contain(action => action.Action == "prefab.resolve" &&
                                          action.PrefabGuid == -2146975260 &&
                                          action.Prefab == "TM_RusticHouse_Roofing_Type6");
    }
}
