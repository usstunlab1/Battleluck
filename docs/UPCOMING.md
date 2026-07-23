# Upcoming Work

## Adaptive drills

- Add richer player combat observations: health, weapon category, movement direction, combat state, buffs, and recent ability effects.
- Add configurable drill reactions for player action patterns, such as ranged cast, weapon swap, dash, and melee approach.
- Add per-event reward budgets, reward audits, and explicit native-drop suppression where the server build exposes drop-table controls.
- Add bounded later-wave adjustment using event performance measurements.

## Data and compatibility

- Import a structured quest catalog before enabling quest objectives, rewards, or progression dependencies.
- Add event-local item/object/schematic strength metadata.
- Validate behavior against each supported V Rising server build and keep optional ProjectM integrations guarded.

## Quality

- Add unit tests for adaptive catalog validation, threat budgets, and command aliases.
- Add server integration tests for spawn ownership, cleanup, and player entity replacement.
