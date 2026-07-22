using BattleLuck.Services.Runtime;

namespace BattleLuck.Tests.Services;

public sealed class RuntimeEffectActionCatalogTests
{
    [Theory]
    [InlineData("effect.assign")]
    [InlineData("zone.border.effect.apply")]
    [InlineData("spawn.effect.assign")]
    [InlineData("tracking.group.effect.apply")]
    public void Runtime_effect_actions_exist_before_harmony_startup(string actionName)
    {
        var manifest = new FakeManifest();
        RuntimeEffectActionCatalog.InjectEntries(manifest.MutableEntries, typeof(FakeEntry));

        Assert.True(manifest.Entries.TryGetValue(actionName, out var entry));
        Assert.True(entry!.HandlerAvailable);
    }

    sealed class FakeManifest
    {
        // Field name and generic shape intentionally match ActionManifestService.
        readonly Dictionary<string, FakeEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
        public System.Collections.IDictionary MutableEntries => _entries;
        public IReadOnlyDictionary<string, FakeEntry> Entries => _entries;
    }

    sealed class FakeEntry
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public string RiskLevel { get; set; } = "";
        public bool RequiresApproval { get; set; }
        public bool HandlerAvailable { get; set; }
        public bool Executable { get; set; }
        public bool MainThreadRequired { get; set; }
        public string Availability { get; set; } = "";
        public List<string> Required { get; } = new();
        public List<string> Optional { get; } = new();
        public List<string> Examples { get; } = new();
    }
}
