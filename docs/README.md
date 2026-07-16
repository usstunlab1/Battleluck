# BattleLuck Documentation

BattleLuck is a V Rising dedicated server BepInEx plugin that adds competitive arena game modes, player state management, zone-driven match flow, and optional AI assistance.

## Quick Start

- **[Installation](user/README.md)** — Install BepInEx, deploy BattleLuck, configure modes
- **[User Commands](user/README.md#commands)** — Player-facing commands
- **[Developer Guide](developer/README.md)** — Architecture, ECS patterns, Harmony patching

## Documentation Structure

| Section | Description |
|---------|-------------|
| [User Guide](user/README.md) | Installation, configuration, commands, game modes |
| [Developer Guide](developer/README.md) | Architecture, ECS patterns, Harmony patching, building |
| [LLM Guide](LLM_GUIDE.md) | Local AI setup, prompting rules, event authoring |
| [Publishing](PUBLISHING_CHECKLIST.md) | Release checklist, Thunderstore, secrets |
| [Deployments](deployments/README.md) | CI/CD, server deployment, AI services |
| [Reference](reference/README.md) | V Rising Mod Wiki references and external tools |

## External References

- [V Rising Mod Wiki](https://wiki.vrisingmods.com/) — Standard mod development patterns
- [Th Thunderstore](https://thunderstore.io/c/v-rising/) — V Rising mod repository
- [Thunderstore Package Guide](https://wiki.thunderstore.io/mods/creating-a-package)

## Project Structure

BattleLuck follows the standard V Rising mod folder layout. See [Mod Structure](developer/mod-structure.md) for details.

```
BattleLuck/
├── BattleLuckPlugin.cs    # BepInEx entry point
├── Core/                  # Static service locator
├── Commands/              # VCF chat commands
├── Services/              # Business logic
├── Patches/               # Harmony patches
├── Models/                # Data structures
├── Utilities/             # Helper methods
├── Data/                  # Static data (PrefabGUIDs, embedded resources)
├── ECS/                   # ECS action infrastructure
├── Events/                # Event bus
└── config/                # Embedded configuration
```

## License

MIT Licensed. See [LICENSE](../LICENSE) and [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).