using System.Reflection;
using BattleLuck.Models;
using Xunit;
using FluentAssertions;

public class ConfigLoaderTests
{
    public ConfigLoaderTests()
    {
    }

    private string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "BL_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void LoadMerchantCommandConfig_MissingOptionalFile_ReturnsDefaults()
    {
        var root = CreateTempRoot();
        try
        {
            var path = Path.Combine(root, "merchant_servant_actions.json");
            // Call internal LoadJson<MerchantServantActionConfig>(optional: true) via reflection
            var mi = typeof(ConfigLoader).GetMethod("LoadJson", BindingFlags.NonPublic | BindingFlags.Static)!;
            var generic = mi.MakeGenericMethod(typeof(MerchantServantActionConfig));
            var result = generic.Invoke(null, new object[] { path, true });
            result.Should().BeNull(); // optional missing returns null
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void LoadAIConfig_MalformedJson_FallsBackToDefaults()
    {
        var root = CreateTempRoot();
        try
        {
            var path = Path.Combine(root, "ai_config.json");
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{ invalid json ");

            // Call internal LoadJson<AIConfig>(optional: false) via reflection; malformed should return null
            var mi = typeof(ConfigLoader).GetMethod("LoadJson", BindingFlags.NonPublic | BindingFlags.Static)!;
            var generic = mi.MakeGenericMethod(typeof(AIConfig));
            var result = generic.Invoke(null, new object[] { path, false });
            result.Should().BeNull();
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
