# Developer Guide

## Starting points

- Use `BattleLuckPlugin` for service initialization and main-thread lifecycle integration.
- Use `SessionController` for event-start/end behavior.
- Add chat/admin behavior through `Commands`, not ad-hoc Harmony command parsing.
- Add configuration contracts under `Models` and load them through `ConfigLoader`.

## NPC and ECS rules

- Spawn only validated prefabs through `SpawnController`.
- Register managed NPCs with `NpcControlService` before issuing follow, aggro, or cleanup operations.
- Observe players by Steam ID and resolve their current character entity at runtime.
- Do not add/remove ECS components while a DOTS system is iterating; defer structural changes to the safe dispatch path.
- Never copy player-only user, input, inventory, or ability-slot components onto an NPC.

## Event changes

Event behavior must be bounded by event configuration: NPC count, threat, rewards, schematics, and cleanup ownership. Keep world quests unsupported until a structured quest catalog exists.

## Verification

```powershell
dotnet build --no-restore
dotnet test --no-build --no-restore
```

Validate against a staging dedicated server and inspect BepInEx logs after plugin load, event start, command execution, disconnect, and event cleanup.
