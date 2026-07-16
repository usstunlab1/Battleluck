# BattleLuck Publishing Checklist

Use this before publishing to GitHub, Thunderstore, or a release zip.

## Package Contents

Required for Thunderstore-style release:

- `BattleLuck.dll`
- `manifest.json`
- `README.md`
- `icon.png` at 256x256 PNG
- `CHANGELOG.md`
- `LICENSE`
- `THIRD_PARTY_NOTICES.md`

Do not include:

- `.env`
- `BepInEx/config/BattleLuck/*.bak`
- `BepInEx/config/BattleLuck/ai_operations.log`
- `node_modules`
- `.llm-chat-history`, `.qwen`, `.claude`, `.vscode`, `.idea`, `.vs`
- model files such as `.gguf`, `.safetensors`, `.bin`
- server player snapshots or live logs

## AI Release Defaults

Published configs should be safe by default:

- Provider: `llama`
- Local endpoint: `http://127.0.0.1:11434`
- Cloudflare/Google providers: disabled
- Discord AI logger: disabled
- Conversation history: disabled
- Event authoring: enabled
- Max event actions: `1000`
- Risky live actions: approval-gated

## Build

```powershell
dotnet restore .\BattleLuck.sln
dotnet build .\BattleLuck.sln --no-restore /p:GenerateReadme=false /p:DeployToServer=false
```

## Secret Scan

```powershell
rg -n "cfat[_]" .
rg -n "cfut[_]" .
rg -n "discord[.]com/api/webhooks" .
rg -n "CLOUDFLARE_AI_API_TOKEN\\s*=\\s*[^\\s#]+"
rg -n "GOOGLE_AI_API_KEY\\s*=\\s*[^\\s#]+"
```

If anything real appears, remove it and rotate the exposed credential.

## Documentation Checks

- README explains BattleLuck is a V Rising BepInEx plugin.
- README says AI is optional and local-first.
- README links to `docs/LLM_GUIDE.md`.
- README credits any referenced mods or libraries.
- LICENSE exists and matches the intended reuse policy.
- Third-party notices mention inspiration or references without copying incompatible code.

## Versioning

Before upload:

- Increment `manifest.json` `version_number`.
- Update `CHANGELOG.md`.
- Keep the package name stable for updates.
- Upload a new zip for any README, DLL, manifest, or config change.

## Final Smoke Test

On a clean server:

```text
.reload
.aistatus
.ai catalog search boss wall glow
.modelist
.event.status
```

For local AI:

```powershell
.\scripts\start_vllm.ps1
```

Fallback profile (llama.cpp):

```powershell
.\scripts\start_local_llama.ps1
```

Then:

```text
.ai.reload
.ai can you search the catalog for boss movement actions?
```

## References

- V Rising Mod Wiki Thunderstore upload guide: https://wiki.vrisingmods.com/dev/upload_to_thunderstore.html
- Thunderstore package guide: https://wiki.thunderstore.io/mods/creating-a-package
- Thunderstore update guide: https://wiki.thunderstore.io/mods/updating-a-package
- BattleLuck LLM guide: LLM_GUIDE.md
