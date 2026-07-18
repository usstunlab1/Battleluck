# BattleLuck — Technology Stack

## Language & Runtime
- **C# with `LangVersion: latest`** (C# 12+ features enabled)
- **Target Framework: `net6.0`** (required by BepInEx IL2CPP for V Rising)
- **Nullable reference types: enabled** (`<Nullable>enable</Nullable>`)
- **Implicit usings: enabled** — additional globals in `GlobalUsings.cs`
- **Unsafe blocks: allowed** (`<AllowUnsafeBlocks>True</AllowUnsafeBlocks>`)
- **Preview features: enabled** (`<EnablePreviewFeatures>true</EnablePreviewFeatures>`)

## Build System
- **MSBuild / .NET SDK** (`Microsoft.NET.Sdk`)
- **Central Package Management** via `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`)
- **Locked restore on CI** (`RestoreLockedMode=true` when `GITHUB_ACTIONS=true`)
- **Isolated intermediate paths** per project via `Directory.Build.props` (`obj\$(MSBuildProjectName)\`)
- **Output type: Library** — produces `BattleLuck.dll`

## Core Dependencies (NuGet)
| Package | Version | Purpose |
|---------|---------|---------|
| `BepInEx.Unity.IL2CPP` | 6.0.0-be.733 | IL2CPP plugin loader for Unity games |
| `BepInEx.Core` | 6.0.0-be.733 | BepInEx core APIs |
| `BepInEx.Unity.Common` | 6.0.0-be.733 | Shared Unity BepInEx utilities |
| `VampireReferenceAssemblies` | 1.1.11-r96495-b8 | V Rising game assembly stubs |
| `Il2CppInterop.Runtime` | 1.4.6-ci.426 | IL2CPP ↔ managed interop |
| `VRising.VampireCommandFramework` | 0.10.4 | Chat command registration and dispatch |
| `HookDOTS.API` | 1.1.1 | Unity DOTS/ECS hook utilities |
| `BouncyCastle.Cryptography` | 2.6.2 | Cryptographic utilities |
| `VAutomationCore` | 1.0.3 | Optional integration (compile-only, not bundled) |

## Test Dependencies
| Package | Version | Purpose |
|---------|---------|---------|
| `xunit` | 2.4.2 | Unit test framework |
| `xunit.runner.visualstudio` | 2.4.5 | VS/Rider test runner |
| `Microsoft.NET.Test.Sdk` | 17.8.0 | Test SDK |
| `FluentAssertions` | 6.12.0 | Assertion library |
| `coverlet.collector` | 6.0.0 | Code coverage |
| `System.Text.Json` | 6.0.10 | JSON (pinned for test project) |

## Key Frameworks & APIs Used
- **Unity ECS (DOTS)** — `Unity.Entities`, `Unity.Mathematics`, `Unity.Collections`, `Unity.Jobs`, `Unity.Transforms`
- **ProjectM** — V Rising's game systems namespace (ECS components, network, prefabs)
- **Stunlock.Core** — V Rising core types (PrefabGUID, etc.)
- **HarmonyLib** — Runtime IL patching for game system hooks
- **Il2CppInterop** — Bridging managed C# to IL2CPP runtime
- **VampireCommandFramework** — Chat command routing with permission levels
- **System.Text.Json** — All JSON serialization (no Newtonsoft)

## Global Usings (`GlobalUsings.cs`)
Available everywhere without explicit `using`:
- All `System.*` collections, IO, threading, JSON, reflection, crypto
- `BepInEx`, `HarmonyLib`, `Il2CppInterop.Runtime`
- `ProjectM`, `ProjectM.Network`, `Stunlock.Core`
- `Unity.Collections`, `Unity.Entities`, `Unity.Jobs`, `Unity.Mathematics`, `Unity.Transforms`, `UnityEngine`
- `VampireCommandFramework`
- All `BattleLuck.*` namespaces (Core, Models, Utilities, ECS, Services, etc.)

## Build Commands
```powershell
# Restore dependencies
dotnet restore

# Build (no deploy)
dotnet build .\BattleLuck.sln -c Release /p:DeployBattleLuck=false

# Build + deploy to local server
dotnet build .\BattleLuck.sln -c Release `
  /p:DeployBattleLuck=true `
  /p:ServerPluginPath="C:\Path\to\VRising_Server\BepInEx\plugins\BattleLuck" `
  /p:ServerConfigPath="C:\Path\to\VRising_Server\BepInEx\config\BattleLuck"

# Run tests
dotnet test .\BattleLuck.Tests\
```

## Deployment Artifacts
When `DeployBattleLuck=true`, the MSBuild `BuildToServer` target copies:
- `BattleLuck.dll` → `$(ServerPluginPath)`
- `BouncyCastle.Cryptography.dll` → `$(ServerPluginPath)`
- `HookDOTS.API.dll` → `$(ServerPluginPath)`
- All `config/BattleLuck/**` files → `$(ServerConfigPath)` (skip unchanged)
- `docs/reference/kindredextract-reference.json` → `$(ServerConfigPath)`

## Embedded Resources
The following are embedded in the DLL and extracted on first server start:
- `config/BattleLuck/**/*.json`, `*.md`, `*.txt`
- `tools/**/*` (PowerShell/shell audit scripts)
- `docs/audit/systems/allowlists/*.json` (KindredExtract ECS allowlists)

## Optional AI Runtime
```yaml
# docker-compose.ai.yml — one-command local Ollama
docker compose -f docker-compose.ai.yml up -d
# Default endpoint: http://127.0.0.1:11434
```
Supported AI providers (configured in `ai_config.json`):
- `llama` / `llama_api` — Ollama or llama.cpp compatible endpoint
- `cloudflare` — Cloudflare AI Workers
- `google` — Google AI Studio
- `auto` — provider auto-selection
- Local fallback (no credentials required)

## CI/CD
- GitHub Actions workflow: `.github/workflows/validate.yml`
- Locked NuGet restore enforced on CI
- Audit/validation scripts in `tools/audit/` and `tools/validators/`

## Solution Projects
| Project | Purpose |
|---------|---------|
| `BattleLuck` | Main plugin (this codebase) |
| `BattleLuck.Tests` | xUnit test project |
| `AssemblyInspector` | Dev tool for ECS assembly inspection |
| `.external/KindredExtract` | Reference ECS extraction tool (not compiled into plugin) |
