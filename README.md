# BattleLuck

BattleLuck is a server-side BepInEx IL2CPP plugin for V Rising dedicated servers. It provides configurable competitive and cooperative game events, managed player sessions, rollback-safe player state, NPC and boss control, progression, death-prevention, teleports, schematics, and an ECS-backed action pipeline.

Optional local AI assists with event authoring, catalog search, and admin guidance. Players receive advice and self-service actions; authenticated admins may describe catalog actions in natural language. Every live mutation is previewed, explicitly confirmed, revalidated, and executed on the server main thread.

## Install

1. Install BepInEx and [VampireCommandFramework](https://thunderstore.io/c/v-rising/p/deca/VampireCommandFramework/) on the dedicated server.
2. Copy the package into `BepInEx/` or use a Thunderstore mod manager.
3. Start the server. The DLL extracts default config files to `BepInEx/config/BattleLuck/`.
4. Edit event definitions in `BepInEx/config/BattleLuck/events/<eventId>/`.
5. Use `.help` in game to see commands for your permission level.

Extraction is additive — the DLL never overwrites existing config, so upgrades preserve server-owner changes.

## AI

AI is server-owned and local-first. No model weights or credentials are bundled in the mod.

- The server reads `ai_config.json` and starts the configured provider or local fallback.
- Use `.ai <question or action description>`; the word `request` is not required.
- Admin descriptions resolve to canonical catalog actions. Live mutations return a preview and require `.ai yes` or `.ai confirm <token>`.
- Example: `.ai spawn a boss at a random position` resolves an allowlisted boss and event-owned position, then waits for confirmation.
- Use `.aistatus` to check provider health; `.ai.reload` to refresh config.
- Hosted AI, Discord, and webhook integrations are opt-in.

See [LLM Guide](docs/LLM_GUIDE.md) for provider setup and the prompt contract.

## Features

- Action-driven event runtime with configurable modes (Bloodbath, Colosseum, custom events).
- NPC control, boss commands, and safe action reachability checks.
- Player event sessions, loadouts, progression, death-prevention charges, and native-backed rollback snapshots.
- Teleport services, schematics, zone detection, and verified data catalogs.
- Optional local LLM for event authoring with approval gates and main-thread-safe execution.
- Server-only action contract: every registered action is server-side, works with unmodified clients, and uses native replication.

### Event and mode terminology

`modeId` and `eventId` refer to the same configuration concept (e.g. `bloodbath`, `colosseum`). The name `modeId` is retained for backward compatibility.

```text
ModeId / EventId = bloodbath          # the configuration
SessionId        = a specific run      # an instance
ZoneHash         = the zone used       # the location
```

## Build

```powershell
dotnet build BattleLuck.sln -c Release

# Deploy to server
dotnet build BattleLuck.sln -c Release /p:DeployBattleLuck=true `
  /p:ServerPluginPath="C:\Path\to\BepInEx\plugins\BattleLuck" `
  /p:ServerConfigPath="C:\Path\to\BepInEx\config\BattleLuck"
```

## Commands

`.help` is the live permission-aware source of truth. The primary interface is `.ai`; all other commands are optional admin tools for events, NPCs, schematics, and integrations.

See the [User Guide](docs/user/README.md) for the full categorized command reference.

## Documentation

- [User guide](docs/user/README.md) — commands, configuration, event creation, troubleshooting
- [Developer guide](docs/developer/README.md) — architecture, ECS patterns, build setup
- [AI and prompt guide](docs/LLM_GUIDE.md) — provider setup, director loop, security
- [V Rising Mod Wiki](https://wiki.vrisingmods.com/)

## Support

Maintainer: **coyoteq1**
Discord: <https://discord.gg/uJ2ehWv4gR>

## License

GNU Affero General Public License v3. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
