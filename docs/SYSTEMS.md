# Systems Tree

```text
BattleLuckPlugin
├── Core
│   ├── ConfigLoader
│   ├── SessionController
│   ├── GameModeRegistry
│   └── SchematicLoader
├── Commands
│   ├── Chat (.ai)
│   └── Admin
├── Services
│   ├── AI (Ollama and safe fallback)
│   ├── Adaptive (event-start combat drills)
│   ├── Npc (tracking, follow, aggro, cleanup)
│   ├── Practice (admin solo drills)
│   ├── Spawn (validated native NPC creation)
│   ├── Runtime (event definitions and validation)
│   └── Flow (event actions and lifecycle)
├── Models
│   ├── Event and session contracts
│   ├── NPC/AI behavior contracts
│   └── Adaptive drill catalog contracts
├── Patches
│   └── Narrow Harmony server hooks
├── Data
│   └── Static prefab/reference data
└── config/BattleLuck
    ├── Events and kits
    ├── AI provider configuration
    └── Adaptive drill catalog
```

Data flows from validated configuration to runtime services. Commands and AI requests do not bypass the event, prefab, permission, or cleanup boundaries.
