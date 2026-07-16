# Qwen Integration (Draft)

This document describes how to integrate the `qwen-code` CLI into the BattleLuck repository for development and MCP usage.

Goals
- Vendorize `qwen-code` as a local dev tool.
- Provide clear bootstrap and auth helpers for developers.
- Add an MCP adapter that exposes the BattleLuck runtime to `qwen`.

Quick start
1. Generate example settings and env files:
```
node ./scripts/qwen-bootstrap.js
```
2. Edit `.qwen/settings.example.json` or copy to `.qwen/settings.json` and set `security.auth.apiKey` or `modelProviders` per your provider.
3. Start the BattleLuck dev server and run `qwen` from `qwen-code` to discover MCP endpoints.

Security
- Never commit real API keys. Use `.qwen/.env` or CI secrets.
- Add `.qwen/` to `.gitignore` if not present.

Next steps
- Implement `mcp-servers/session-runtime` adapter to map `ISessionRuntimeService` DTOs to MCP endpoints used by `qwen`.
- Add CI job to run `qwen-code` tests and integration checks.
