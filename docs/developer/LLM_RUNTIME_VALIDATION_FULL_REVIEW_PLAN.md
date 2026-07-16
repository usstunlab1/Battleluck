# LLM Runtime Validation Full Review Plan

## Goal
Ensure every LLM-produced action is both:
1. Registered in BattleLuck action catalogs/handlers.
2. Validated against live game runtime state (prefabs, tracked NPC/boss/session context) before execution.

## Scope Reviewed (File-by-File)

### 1) Services/AI/AiGroupProjectMLlmBridge.cs
Status: COMPLETED (core gate implemented)

What is already enforced:
- Allowlist and per-action policy checks.
- Active session requirement checks (policy-driven).
- Manifest registration + required-parameter validation via ActionManifestService.
- Live prefab validation for prefab-like parameters.
- Live NPC validation when npcId is provided.
- Live boss/session registry validation for boss/ai.boss/ai.set_behavior actions.
- Cooldown check occurs only after validation so rejected actions do not consume cooldown.

Result:
- ProjectM auto-execute directives now fail closed when runtime references are invalid.

---

### 2) Services/Flow/FlowActionExecutor.cs
Status: COMPLETED (existing baseline)

What is already enforced:
- Action string parsing + normalization.
- Registration gate (action must be in registered set via catalog integration).
- Runtime handler availability via ExecuteParsed switch path.

Result:
- Non-registered actions are blocked globally at executor level.

---

### 3) Services/Runtime/ActionManifestService.cs
Status: COMPLETED (existing baseline)

What is already enforced:
- Catalog-driven action normalization, aliases, and metadata.
- Required parameter validation.
- Handler availability checks.

Gap:
- This validates schema/registration but does not validate runtime world state by itself (expected).

---

### 4) Utilities/PrefabHelper.cs
Status: COMPLETED (existing baseline used by bridge)

What is available:
- Deep live-prefab resolution helpers.
- Strict and deep validation paths against live prefab map.

Result:
- Runtime prefab checks are available and now wired in ProjectM bridge path.

---

### 5) Services/AI/LiveEventOperatorService.cs
Status: PARTIAL (important gap)

What is already enforced:
- Event definition validation.
- Action manifest validation for action names and required parameters.
- Approval-gated execution model.

What is missing:
- Pre-execution live runtime validation equivalent to AiGroup bridge checks.
- Explicit live validation pass for each approved live action (prefabs, tracked NPC/boss/session checks).

Risk:
- AI-approved live actions can pass catalog checks but still reference invalid runtime entities/prefabs.

---

### 6) Commands/PlayerCommands.cs
Status: PARTIAL

What is already present:
- ai.group.status, ai.group.auto, ai.group.policy operational controls.
- Bridge status exposes latest execution result.

What is missing:
- Dedicated admin command to run dry-run validation for arbitrary action strings without execution.
- Explicit reason/category output (registration vs prefab vs npc vs boss/session) for operator debugging.

---

## Critical Findings (Nothing Hidden)

1. Covered path
- ProjectM LLM auto-execute path is now strict and runtime-aware.

2. Not fully covered path
- LiveEventOperatorService live action approval path still lacks equivalent runtime object/prefab/session validation before executeAction callback.

3. Cross-path consistency issue
- Validation logic is currently bridge-local; should be extracted into a shared validator so all LLM execution entry points enforce the same contract.

4. Test gap
- No focused tests currently prove rejection behavior for invalid live prefab/npc/boss references across all AI execution paths.

## Full Implementation Plan (to ensure nothing missing)

### Phase A: Unify runtime validation
1. Create a shared validator service (suggested: Services/Runtime/LlmRuntimeActionValidator.cs).
2. Move preflight rules from AiGroup bridge into shared service.
3. Reuse in:
   - AiGroupProjectMLlmBridge auto-execute
   - LiveEventOperatorService.ApproveLiveActions
   - Any future LLM direct execution endpoint

Acceptance criteria:
- Same action string validated identically regardless of which AI path executes it.

### Phase B: Complete live action approval safety
1. In LiveEventOperatorService.ApproveLiveActions, call shared runtime validator for each action before executeAction.
2. Fail approval early with actionable rejection messages.
3. Keep existing manifest validation (do not remove), add runtime layer on top.

Acceptance criteria:
- Live action approval rejects invalid runtime references before any side effects.

### Phase C: Operator diagnostics
1. Add a command (example: ai.group.validate <action>) that runs full preflight and returns category-tagged result.
2. Add short rejection category tagging (REGISTRATION, PREFAB, NPC, BOSS, SESSION, COOLDOWN).

Acceptance criteria:
- Admin can diagnose why an LLM action is blocked without forcing execution.

### Phase D: Tests and verification
1. Add unit/integration tests for validator:
   - Unknown action.
   - Missing required param.
   - Invalid prefab.
   - Unknown/dead npcId.
   - Unknown/dead boss target in active session.
2. Add tests for LiveEventOperatorService approval rejection path.
3. Keep current build validation and add test run in CI path.

Acceptance criteria:
- Tests fail if any AI path bypasses runtime validator.

## Suggested File Change List (Next Patch)

Mandatory:
- Services/Runtime/LlmRuntimeActionValidator.cs (new)
- Services/AI/AiGroupProjectMLlmBridge.cs (refactor to shared validator)
- Services/AI/LiveEventOperatorService.cs (enforce shared validator before executeAction)

Recommended:
- Commands/PlayerCommands.cs (validation diagnostic command)
- Tests/... (new validator and approval-path coverage)

## Verification Checklist

- [x] Build passes after bridge hardening.
- [x] ProjectM path blocks unregistered/invalid runtime actions.
- [ ] LiveEventOperatorService live-action approval uses shared runtime validator.
- [ ] Cross-path parity verified (same action => same validation outcome).
- [ ] Automated tests cover runtime reference failures.
- [ ] Admin-facing diagnostics command available.

## Decision
Current state is significantly improved and safe for ProjectM auto-execute path, but full "all LLM registrations and validations from game itself" is not fully complete until LiveEventOperatorService and other LLM execution entries reuse the same runtime validator.
