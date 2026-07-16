# KindredExtract Unity system/tick research prompt

You are reviewing `kindredextract-systems.csv`, generated from the
[Odjit/KindredExtract](https://github.com/Odjit/KindredExtract) reference list
for a V Rising Unity ECS server. The CSV contains 1534 system/type names and
name-based hints only; it is not proof of runtime scheduling.

For each system, research and verify:

1. The full Unity/ProjectM type and whether it exists in the server world.
2. The actual purpose and the components/queries it reads or writes.
3. The real update group (`InitializationSystemGroup`, simulation group,
   fixed-step group, presentation group, or a ProjectM group), ordering, and
   whether it runs server, client, or shared.
4. Whether it is safe to observe from a server plugin and the correct tick/main
   thread boundary for an approved BattleLuck action.

Do not infer an exact tick rate from a name. Mark unknown values as `unknown` and
cite the assembly/source or a live KindredExtract dump. Keep the output bounded:
return a corrected CSV with the original `system_name`, verified purpose,
`world`, `update_group`, `order`, `tick_semantics`, `evidence`, and
`confidence` columns. Never propose arbitrary reflection or direct mutation of
an unverified native system.

Timing for BattleLuck sequences must use validated `wait:<seconds>` and
`tick:<event-second>` markers; the server main-thread dispatcher remains the
mutation boundary.
