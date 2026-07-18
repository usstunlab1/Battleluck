# BattleLuck — Project Structure

## Root Layout
```
BL/
├── BattleLuckPlugin.cs          # BepInEx plugin entry point; owns Load/Unload/ServerTick
├── GlobalUsings.cs              # Global using directives for the entire assembly
├── BattleLuck.csproj            # .NET 6 SDK project; embeds config/tools as resources
├── BattleLuck.sln               # Solution file (main + Tests + AssemblyInspector)
├── Directory.Build.props        # Shared MSBuild properties
├── Directory.Packages.props     # Central NuGet package version management
├── Commands/                    # VampireCommandFramework chat command handlers
├── Core/                        # Bootstrap, session orchestration, validation
├── Data/                        # Static prefab/GUID catalog
├── ECS/                         # Unity ECS action components, systems, queries, events
├── Events/                      # Internal C# event bus and gameplay event definitions
├── Models/                      # Plain data models / DTOs (no game logic)
├── Patches/                     # Harmony IL2CPP patches
├── Services/                    # All runtime service implementations
├── Utilities/                   # Cross-cutting helpers (logging, entity extensions, etc.)
├── config/BattleLuck/           # Default config files embedded as resources
├── docs/                        # Developer, user, reference, and audit documentation
├── tools/                       # PowerShell/shell audit and validation scripts
├── .external/KindredExtract/    # Reference-only ECS extraction tool (not compiled into plugin)
└── ai-assets/                   # Optional Python AI sidecar (Dockerfile + app.py)
```

## Core Layer (`Core/`)
| File | Responsibility |
|------|---------------|
| `Core.cs` | Static service locator; holds all singleton service references |
| `VRisingCore.cs` | Wraps Unity `EntityManager` and online player enumeration |
| `SessionController.cs` | Top-level tick orchestrator; drives FlowController and ZoneDetection |
| `GameModeRegistry.cs` | Registers and looks up `GameModeEngine` instances by mode ID |
| `ConfigLoader.cs` | Loads JSON configs from `BepInEx/config/BattleLuck/`; deploys defaults on first run |
| `ModeConfigLoader.cs` | File-watcher for hot-reload of mode configs |
| `ExecutionPipeline.cs` | Synchronous inline action execution pipeline |
| `MainThreadDispatcher.cs` | Thread-safe queue; drains on each server tick |
| `SchematicLoader.cs` | Loads and tracks arena schematics |
| `Loaders/ConfigAdapter.cs` | JSON deserialization helpers |
| `Validation/` | Pre-flight validators: actions, analytics, flow, kits, prefabs, schematics, sequences, tech, zones |

## Commands Layer (`Commands/`)
Organized by feature area. All handlers use `VampireCommandFramework` attributes.
```
Commands/
├── Admin/          # AdminCommands, DevCommands, EventDeployment, LlmAnalytics, Rollback, Roadmap, SystemReference
├── Attributes/     # BattleLuckCommandAttribute (permission tagging)
├── Boss/           # BossCommands, NpcCommands, DataExportCommands
├── Converters/     # ActionArgumentConverters (VCF type converters)
├── Flow/           # ActionsConsoleCommands, FlowCommands
├── Game/           # ModeCommands, MutatorCommands, TeamCommands
├── Player/         # PlayerCommands
├── BattleLuckCommandDispatcher.cs  # Scans and registers all command classes
└── CommandContextExtensions.cs     # Extension helpers on VCF ChatCommandContext
```

## Services Layer (`Services/`)
The largest layer; grouped by domain:

| Subfolder | Domain |
|-----------|--------|
| `AI/` | AIAssistant, provider clients (Llama, Cloudflare, Google, Sidecar), conversation store, intent router, action planner, MCP runtime |
| `Cleanup/` | SessionCleanupService — destroys event entities on mode end |
| `Flow/` | FlowController, FlowActionExecutor, FlowPersistence, DevSessionService, EcbHelper |
| `Integrations/` | DiscordBridgeController, WebhookController, MCPRuntimeService |
| `Logistics/` | LogisticsController |
| `Modes/` | GameModeBase, GameModeEngine |
| `Npc/` | NpcControlService, NpcActionAuditor |
| `Progression/` | ProgressionController |
| `Runtime/` | ActionRegistry, ActionValidationPipeline, EventDefinitionLoader, EventDeploymentService, EventRuntimeController, SnapshotService, CustomSequenceService, LiveSystemRegistryService, OperatorSafetyService, and ~30 more runtime services |
| `Spawn/` | SpawnController, WaveController, LootCrateController |
| `Zone/` | BorderWallController, ShrinkZoneController, GlowBorderController, TimerController, PlatformController, ZoneMapIconService, and more |
| Root services | KitController, TeleportService, PlayerStateController, PlayerLoadoutService, DeathPreventionService, EloController, TeamBalancer, etc. |

## ECS Layer (`ECS/`)
```
ECS/
├── Actions/
│   ├── Components/   # ECS component structs for action state
│   ├── Systems/      # ECS systems that process action components
│   └── ActionSystems.cs
├── Commands/         # ECS command helpers
├── Events/           # ActionCompletedEvent, ProjectMEvents, ZoneEvents
├── Queries/          # QueryDefinition, QueryRegistry (cached EntityQuery pool)
└── VSystemBase.cs    # Base class for BattleLuck ECS systems
```

## Models Layer (`Models/`)
Pure data classes / records / DTOs. No game logic. Key files:
- `EventDefinitionModels.cs` — event flow, phases, timers, triggers
- `ActionModels.cs` / `ActionCatalogModels.cs` — action descriptors and catalog entries
- `PlayerEventSession.cs` — per-player session state
- `SnapshotModels.cs` — rollback snapshot data
- `AIConfig.cs` — AI provider configuration
- `ModeConfig.cs` / `RulesConfig.cs` — game mode rules

## Patches Layer (`Patches/`)
Harmony `[HarmonyPatch]` classes that hook into V Rising game systems:
- `ServerTickHook.cs` — drives `BattleLuckPlugin.ServerTick` each game tick
- `ChatMessageSystemPatch.cs` — intercepts chat for `.` command routing
- `DeathHook.cs` — fires `GameEvents.OnPlayerDied`
- `InitializationPatch.cs` — triggers `TryInitializeCore` when server world is ready
- `ZoneDetectionSystem.cs` — spatial zone enter/exit detection
- `ProjectMEventRouterPatches.cs` — routes ProjectM ECS events to `ProjectMEventRouter`

## Config Files (`config/BattleLuck/`)
All embedded as resources in the DLL; extracted on first run (additive, never overwrites):
```
config/BattleLuck/
├── events/<eventId>/    # flow.json, zones.json, kits.json, prompt.txt per event
├── prompts/             # LLM system prompts
├── sequences/           # Named reusable action sequences
├── schematics/          # Arena schematic captures
├── actions_catalog.json # 202 registered action names + metadata
├── ai_config.json       # AI provider settings
├── ai_operator_prompt.md
├── kit_grant_rules.json
├── operator_safety.json
├── roadmap.json
└── webhook.json
```

## Architectural Patterns

### Service Locator (Core.cs)
All singleton services are stored in `Core` static fields. `BattleLuckPlugin` exposes forwarding properties for backward compatibility. Services are created in `TryInitializeCore()` after the V Rising world is ready.

### Main-Thread Dispatch
All Unity ECS / ProjectM mutations must run on the server main thread. Async work (AI HTTP calls, Discord, webhooks) enqueues lambdas via `MainThreadDispatcher.Enqueue()`; `ServerTick` drains the queue each frame.

### Approval-Gated Actions
AI-proposed mutations follow: catalog search → preview (creates proposal ID) → admin inspect → explicit approve → main-thread execution. Rollback discards pending proposals only; it cannot undo already-executed actions.

### Declarative Event Config
Events are defined entirely in JSON. `EventDefinitionLoader` parses them; `EventRuntimeController` executes phases, timers, and triggers. No C# subclassing required for new events.

### Harmony Patching Strategy
`PatchAll` is attempted first; on failure, critical patch classes are applied individually so a missing type in one class doesn't block others.

### Embedded Resource Deployment
Config files, tools, and audit allowlists are embedded as `EmbeddedResource` in the DLL. `ConfigLoader.EnsureDefaultsDeployed()` extracts them on first load without overwriting existing files.
