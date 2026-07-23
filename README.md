# BattleLuck

BattleLuck is a server-side V Rising event plugin built with BepInEx, IL2CPP interop, Harmony, and VampireCommandFramework.

## Features

- Config-driven events, arenas, kits, rewards, and controlled NPCs.
- Local Ollama-backed `.ai` assistant with a safe fallback.
- Adaptive combat drills that calculate a bounded NPC spawn plan from active event participants.
- Admin-only solo practice modes: follow, fight, and close-follow mirror.

## Requirements

- A V Rising dedicated server compatible with the package references in `BattleLuck.csproj`.
- BepInEx and VampireCommandFramework installed on the server.
- .NET 6 SDK for local development.

## Build

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build --no-restore
```

## Documentation

- [Administration](docs/ADMIN.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Development](docs/DEVELOPMENT.md)
- [Developer guide](docs/DEVELOPER_GUIDE.md)
- [GPT Dashboard integration](docs/GPT_DASHBOARD.md)
- [Systems tree](docs/SYSTEMS.md)
- [Upcoming work](docs/UPCOMING.md)
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)

## Safety

BattleLuck runs server-side. It never creates a substitute player entity or injects client input. AI entities remain native NPCs controlled through server-visible state, validated prefabs, and bounded event configuration.
