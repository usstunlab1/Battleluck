# BattleLuck — Product Overview

## Purpose
BattleLuck is a server-side BepInEx IL2CPP plugin for V Rising dedicated servers. It provides a structured, config-driven competitive and cooperative game event framework with an optional AI assistant layer, all running entirely on the server — players install nothing.

## Value Proposition
- Server owners define events through JSON config files; no C# code required for new game modes.
- All AI, approval, rollback, and action pipeline logic runs server-side and is local-first.
- Upgrades are additive: the DLL never overwrites existing config, event, prompt, or tool files.
- Cloud-provider-neutral: runs on any Windows or Linux V Rising dedicated server.

## Key Features

### Game Events & Modes
- Match-ready, action-driven event flow for arena and custom V Rising events (Bloodbath, Siege, Trials, Colosseum, AI Event).
- Declarative event definitions via `flow.json`, `zones.json`, `kits.json`, `prompt.txt` per event folder.
- `.event.create <id> [template]` clones a full event without writing C#.
- Phase timers, triggers, sequences, and tick-scheduled actions.

### Player Management
- Per-player event sessions, loadouts, progression, and death-prevention charges.
- Native-backed rollback snapshots and restore-on-exit flows.
- ELO/Colosseum rating, scoreboard, team balancing.

### NPC & Boss Control
- Spawn, follow, move, and despawn controlled NPCs and bosses via commands or event actions.
- NPC action auditing and simulation models.

### AI Assistant (`.ai` command surface)
- Server-owned, local-first: supports Ollama/llama.cpp, Cloudflare AI Workers, Google AI Studio, or a simple local fallback.
- Player chat is advice-only; admin commands create approval-gated previews before any mutation.
- Catalog-backed action planning, event authoring, sequence creation, and config editing.
- Optional per-player AI chat backups (JSONL, server-side, opt-in).
- MCP (Model Context Protocol) runtime integration for tool-calling.

### Spatial & Zone Services
- Teleport services, spatial points, borders, schematics, and zone map icons.
- Border walls, glow borders, shrink zones, floor locks, platform control.
- Schematic capture and replay for arena layouts.

### Integrations (opt-in)
- Discord bridge (log forwarding, VIP chat channel).
- External webhook endpoint.
- AI logger (game event → AI summary → Discord webhook).
- Docker Compose one-command local AI runtime (`docker-compose.ai.yml`).

### Action Pipeline
- 202 registered action names, 34 example categories, built-in sequence definitions.
- Every action must have a runtime handler, pass validation, and be approval-gated.
- `system.*` aliases record verified ProjectM/Unity ECS system references.
- Main-thread-safe execution via `MainThreadDispatcher`; async AI work dispatched back to game thread.

## Target Users
- **Server owners / admins**: configure events, manage AI provider, approve live actions.
- **Players**: read-only `.ai` advice, event participation, scoreboard/ELO queries.
- **Mod developers**: extend action catalog, add ECS systems, author new event templates.

## Primary Commands
```
.help                    Permission-aware command list
.ai <message>            Server-side AI advice (public, read-only)
.aistatus / .ai.status   Provider health
.ai catalog search       Search verified action catalog (admin)
.ai action <name>        Preview a runtime action (admin)
.ai approve [id]         Execute an approved proposal (admin)
.event.create <id>       Clone an event template (admin)
.reload                  Reload all BattleLuck configuration (admin)
```

## Versioning & Distribution
- BepInEx plugin GUID: `gg.battleluck`, version `1.0.0`.
- Distributed via Thunderstore-compatible mod manager or manual DLL copy.
- License: GNU AGPL v3 or later.
