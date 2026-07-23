# Development

## V Rising modding rules

- Target the installed V Rising server build and matching IL2CPP interop assemblies.
- Use BepInEx for plugin loading, Harmony only for narrow lifecycle hooks, and VampireCommandFramework for public commands.
- Validate prefab IDs against the live prefab collection before use.
- Keep gameplay changes server-side and avoid relying on client input, UI, or animations.
- Never mutate entity structure during active DOTS iteration; queue structural changes through the project safe-sync path.

## Verification

```powershell
dotnet build --no-restore
dotnet test --no-build --no-restore
```

Exercise new commands on a staging server and inspect BepInEx logs after server startup, event start, event end, and player disconnect.

## Data changes

Use JSON under `config/BattleLuck` for server-owner tuning. Preserve the audit allowlists and KindredExtract reference assets under `docs/`; they support validation and are embedded by the project build.
