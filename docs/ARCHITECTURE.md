# Architecture

## Server lifecycle

`BattleLuckPlugin` initializes V Rising services after the server is ready. `SessionController` owns event entry, readiness, start, end, and cleanup. At event start, the adaptive drill service derives a bounded plan, stores it in session state, and spawns only catalog-approved NPCs.

## Project layout

| Folder | Responsibility |
| --- | --- |
| `Commands/` | VampireCommandFramework and chat entry points. |
| `Core/` | Session lifecycle, configuration, and event orchestration. |
| `Services/` | Runtime behavior, AI, NPC control, spawning, and integrations. |
| `Models/` | Serializable configuration and runtime data contracts. |
| `Patches/` | Narrow Harmony hooks. |
| `Data/` | Static prefab/reference data. |
| `config/BattleLuck/` | Server-editable event and AI configuration. |
| `docs/audit` and `docs/reference` | Build-required validation/reference assets. |

## ECS safety

Player and NPC entities are distinct. Player entities are observed by stable Steam ID plus a refreshable character entity reference. NPCs retain native faction, aggro, movement, and ability systems. Structural ECS changes must be deferred to a safe synchronization point.

## Adaptive drill flow

1. Resolve the active event and participants.
2. Derive bounded combat strength from server-visible level/state.
3. Select catalog-approved NPC entries under the event threat and count limits.
4. Spawn and register native NPCs.
5. Apply native follow or aggro behavior and clean them up with the session.

The current mirror mode is close-follow behavior. Exact client input, player spell slots, and animation replication are intentionally outside the server-side design.
