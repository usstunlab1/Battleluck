# Implementation Progress

## Phase 1: Extend Action Definitions
- [ ] Extend `actions_generation.json` with new actions (doors, boss AI, traps, mounts, zone buffs, sequences, revive, objectives, walls, autotrash)
- [x] Add new ECS Action components for all new actions
- [x] Add new ActionType enum values

## Phase 2: Flow Controller & Compiler Updates
- [ ] Extend `FlowController.cs` with new action handlers
- [ ] Extend `FlowCompiler.cs` with new action materialization
- [x] Add new utility controllers (DoorController, ReviveController, TrapController, MountController)

## Phase 3: Config Updates
- [ ] Update `session.json` configs for all modes
- [ ] Update `flow_enter.json` / `flow_exit.json` fallback files
- [ ] Update `ModeConfig` / `SessionConfig` models with new fields

## Phase 4: Documentation & Cleanup
- [ ] Update README.md
- [ ] Update ECS_TRANSFORMATION_SUMMARY.md
- [ ] Clean up unused deprecated code

## Completed Actions
All 78 flow actions are now fully implemented in FlowActionExecutor.cs with:
- Core actions (snapshot, kit, heal, teleport, buff, ability, PvP, blood)
- Spawn actions (wave, boss, NPC, structure, tile, wall, floor)
- Door actions (open, close, lock, unlock) - best-effort state tracking
- Boss/AI actions (follow, aggro, deaggro, behavior, spawn_group)
- Trap actions (place, trigger, remove)
- Mount actions (summon, dismiss, slowdown)
- Zone buff actions
- Sequence/glow actions
- Auto teleport/fly actions
- Revive actions (grant, consume, reset)
- Objective actions (capture, complete, reset)
- Player upgrade/downgrade
- Equipment restriction
- Timer/score actions
- Notification/marker/faction/death prevention actions
