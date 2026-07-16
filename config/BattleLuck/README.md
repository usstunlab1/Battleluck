# BattleLuck Configuration

This directory contains all configuration files for the BattleLuck game mode system.

## Directory Structure

- **core/** - Global/shared configs (actions, abilities, kits, bosses)
- **integrations/** - External service configs (AI, Discord, webhooks)
- **modes/** - Game mode definitions (bloodbath, colosseum, siege, trials, aievent)
- **schematics/** - Arena geometry blueprints
- **sequences/** - Custom action sequences
- **profiles/** - Test/dev client profiles
- **docs/** - Documentation and guides

## Creating a New Mode

1. Copy `modes/_template/` to `modes/your_mode_name/`
2. Edit `manifest.json` with your mode metadata
3. Configure `session.json` (rules, duration, player limits)
4. Define `zones.json` (arena positions, boundaries)
5. Set up `kit.json` (starting equipment)
6. Create `flow_enter.json` and `flow_exit.json` (entry/exit actions)
7. (Optional) Add `event.json` for complex event flows
8. Register mode in `BattleLuckPlugin.cs`

## Config Loading Order

1. Core configs loaded at plugin startup
2. Mode configs loaded on first `.toggleenter <mode>`
3. Schematics loaded when arena initializes
4. Event definitions loaded when session starts

## Validation

Run `.validateconfig <mode>` in-game to check for:
- Missing required files
- Invalid JSON syntax
- Broken action references
- Missing schematic files

## Live ProjectM / Unity System References

Admins can register a verified system from the bundled KindredExtract inventory without restarting the plugin:

`.bl.system.register ProjectM ProjectM.AbilityInputSystem system.projectm.ability_input Ability input reference`

Registrations are saved in `live_system_registry.json` and become available immediately as live actions. They are verified references for BattleLuck and AI tooling; they do not instantiate, patch, or invoke arbitrary native ECS systems.

## Backup

Configs are automatically backed up before major changes.

## Support

See `docs/config_schema.md` for detailed format reference.
