# Administration

## AI runtime

BattleLuck’s default local provider is Ollama at `http://127.0.0.1:11434` using `qwen2.5:0.5b`. Run Ollama on the V Rising server host, then use `.ai.reload` and `.aistatus` after the API is available.

### Optional GPT / OpenAI provider

The OpenAI provider is disabled by default. Set the server environment variable `BATTLELUCK_OPENAI_API_KEY` to a project API key, set `provider` to `openai` in `config/BattleLuck/ai_config.json`, and run `.ai.reload`. Optional environment variables are `BATTLELUCK_OPENAI_MODEL`, `BATTLELUCK_OPENAI_BASE_URL`, and `BATTLELUCK_OPENAI_TIMEOUT_SECONDS`.

Do not place a real key in `ai_config.json`, a command, chat, a log, or source control. With `provider: "auto"`, BattleLuck tries local Ollama first and then OpenAI when it is configured. `.aistatus` reports the active provider and model without exposing the key.

## Chat commands

| Command | Purpose |
| --- | --- |
| `.ai <request>` | Send an assistant request. |
| `.ai r <request>` | Short request alias. |
| `.ai pr fol` | Start an admin practice follower. |
| `.ai pr fig` | Start an admin practice opponent. |
| `.ai pr mir` | Start close-follow mirror behavior. |
| `.ai pr st` | Show practice status. |
| `.ai pr stop` | Despawn the practice NPC. |

Practice commands are admin-only. They create native NPC entities; player controls, equipment slots, and input sequences are never copied.

## Adaptive drills

`config/BattleLuck/adaptive_drills.json` controls automatic event-start drill spawning. Each event can declare allowed NPC prefabs, threat costs, strength ranges, maximum NPC count, and behavior. The `*` entry is the fallback for events without a dedicated profile.

Keep NPC and reward choices event-configured. Quests and canonical progression mapping are unsupported until a structured catalog is supplied.
