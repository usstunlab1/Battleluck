# BattleLuck Issues & Action Items

Generated from DEEP_REVIEW.md. Use this to create GitHub issues or Jira tickets.

---

## 🔴 CRITICAL (Blocking Production)

### ISSUE-001: Revoke & Regenerate Exposed Cloudflare API Token
**Severity:** CRITICAL  
**Status:** ⚠️ USER ACTION REQUIRED

**Description:**
The CLOUDFLARE_API_TOKEN was exposed in a chat session. All instances of this token must be revoked and a new token generated.

**Acceptance Criteria:**
- [ ] Revoke token at https://dash.cloudflare.com/profile/api-tokens
- [ ] Generate new token with same scopes
- [ ] Update GitHub Secrets: CLOUDFLARE_API_TOKEN
- [ ] Verify CI workflow runs successfully with new token
- [ ] Confirm old token is no longer accepted (test API call)

**Related:**
- docs/CI_SECRETS.md (local setup)
- Utilities/CloudflareClientExample.cs (usage example)

---

### ISSUE-002: Audit & Disable AllowUnsafeBlocks in csproj
**Severity:** CRITICAL  
**Status:** 📋 PENDING

**Description:**
The BattleLuck.csproj enables unsafe code blocks, but no unsafe code is currently used. This increases the attack surface. Verify no unsafe code exists, then disable the flag.

**Acceptance Criteria:**
- [ ] Search codebase: `grep -r "unsafe\|fixed(" --include="*.cs"`
- [ ] Confirm no unsafe code blocks exist
- [ ] If none found: set `<AllowUnsafeBlocks>False</AllowUnsafeBlocks>` in csproj
- [ ] Build and test to ensure no errors
- [ ] If unsafe code *is* used: document why and add code review gate

**Files:**
- BattleLuck.csproj (line 6)

---

### ISSUE-003: Implement Webhook Signature Validation
**Severity:** CRITICAL  
**Status:** 📋 PENDING

**Description:**
External webhook endpoints (Discord, Lark, custom webhook) do not validate request signatures. Untrusted sources can spoof events, leading to false game state changes.

**Acceptance Criteria:**
- [ ] **Discord:** Implement Ed25519 signature validation (X-Signature-Ed25519 header)
- [ ] **Lark:** Implement Lark webhook signature validation (if applicable)
- [ ] **Custom Webhook:** Add configurable HMAC-SHA256 validation
- [ ] Return 401 for invalid signatures
- [ ] Log signature validation failures
- [ ] Unit tests for signature validation (mocked requests)

**Files to Update:**
- Services/Integrations/DiscordBridgeController.cs
- Services/Integrations/LarkBridgeService.cs
- Services/Integrations/WebhookController.cs

**References:**
- https://discord.com/developers/docs/interactions/receiving-and-responding#security

---

### ISSUE-004: Fix Exception Logging (Use ex.ToString() not ex.Message)
**Severity:** CRITICAL  
**Status:** 📋 PENDING

**Description:**
Throughout the codebase, catch blocks log only `ex.Message`, losing stack traces essential for debugging and security incident response.

**Example (BEFORE):**
```csharp
catch (Exception ex) {
    Log?.LogWarning($"[BattleLuck] Tick error: {ex.Message}");  // ❌ Only message
}
```

**Example (AFTER):**
```csharp
catch (Exception ex) {
    Log?.LogWarning($"[BattleLuck] Tick error:\n{ex}");  // ✅ Full exception
}
```

**Acceptance Criteria:**
- [ ] Find all catch blocks with `ex.Message` (grep: `ex\.Message`)
- [ ] Replace with `ex.ToString()` or `ex` (implicit ToString)
- [ ] Verify log output includes stack trace
- [ ] Update BattleLuckPlugin.cs (lines 164, 173, 182, etc.)
- [ ] Add pre-commit hook or lint rule to prevent regression

**Estimated Count:** 40+ instances

---

## 🟠 HIGH (Next Sprint)

### ISSUE-005: Add Unit Tests for Core Systems (Target: 30% Coverage)
**Severity:** HIGH  
**Status:** 📋 PENDING  
**Effort:** L (Large, 2–3 sprint)

**Description:**
Current test coverage is ~0.5% (1 test file). Add unit tests for critical paths to prevent regressions and ease refactoring.

**Phase 1: Foundation (Sprint 1)**
- [ ] Set up test project: BattleLuck.Tests (xUnit or NUnit)
- [ ] Create test utilities: mocks for EntityManager, Log, SessionContext
- [ ] Add CI job to run tests + coverage reporting
- [ ] Document test naming convention (UnitOfWork_Scenario_ExpectedResult)

**Phase 2: SessionController Tests (Sprint 1–2)**
- [ ] Test session lifecycle: Create → Start → End → Cleanup
- [ ] Test player join/leave during active session
- [ ] Test edge case: player disconnects without `.toggleleave`
- [ ] Test cleanup on mode end (zone verification)
- **Target:** 10+ tests, ~500 lines

**Phase 3: FlowActionExecutor Tests (Sprint 2)**
- [ ] Test action string parsing (e.g., "kit.apply:kitId=sword_master|modeId=bloodbath")
- [ ] Test parameter validation (missing/invalid params)
- [ ] Test action execution rollback on error
- [ ] Test ordering constraints (e.g., snapshot must be saved before restore)
- **Target:** 8+ tests, ~400 lines

**Phase 4: ScoreTracker & EloController (Sprint 2–3)**
- [ ] Test ranking calculations (Elo, win/loss)
- [ ] Test tie-breaking rules
- [ ] Test serialization/deserialization
- **Target:** 6+ tests, ~300 lines

**Files:**
- Tests/ (new directory)
- BattleLuck.Tests.csproj (new)

---

### ISSUE-006: Refactor Global Statics to Dependency Injection
**Severity:** HIGH  
**Status:** 📋 PENDING  
**Effort:** XL (4+ sprints)

**Description:**
BattleLuckPlugin has 40+ static fields making the code hard to test, thread-unsafe, and tightly coupled. Use constructor injection and factory patterns.

**Phase 1: Create ServiceContainer (Sprint 1)**
- [ ] Design `IServiceContainer` interface with Resolve<T>()
- [ ] Implement simple DI container or use existing library (Microsoft.Extensions.DependencyInjection)
- [ ] Register all services at plugin Load()
- [ ] Thread-safe lazy initialization

**Phase 2: Refactor BattleLuckPlugin (Sprint 2–3)**
```csharp
// BEFORE
public static SessionController? Session { get; private set; }

// AFTER
public static IServiceContainer? Services { get; private set; }
public static SessionController Session => Services?.Resolve<SessionController>();
```

**Phase 3: Update Tests (Sprint 3–4)**
- [ ] Mock IServiceContainer in tests
- [ ] Inject dependencies instead of using statics

**Files:**
- Core/ServiceContainer.cs (new)
- BattleLuckPlugin.cs (major refactor)
- All service consumers

---

### ISSUE-007: Add Hot-Reload Config Validation
**Severity:** HIGH  
**Status:** 📋 PENDING

**Description:**
The `.reload` command reloads configs from disk without validating compatibility with running sessions. Bad configs can corrupt game state.

**Acceptance Criteria:**
- [ ] Parse new config file before applying
- [ ] Validate all required fields present (session.json, zones.json, kit.json)
- [ ] Check for schema violations (e.g., duplicate zone hashes, invalid mode IDs)
- [ ] If invalid: reject reload with helpful error message
- [ ] Log all config changes (before/after diff)
- [ ] Queue config changes for next session end (safe transition)

**Files:**
- Core/ConfigLoader.cs
- Commands/AdminCommands.cs (`.reload` command handler)

---

### ISSUE-008: Add Retry Logic & Circuit Breaker for External APIs
**Severity:** HIGH  
**Status:** 📋 PENDING

**Description:**
API calls to Cloudflare, Discord, and Lark may fail due to network issues. Implement exponential backoff and circuit breaker to improve resilience.

**Acceptance Criteria:**
- [ ] Create `IRetryPolicy` interface with exponential backoff
- [ ] Create `ICircuitBreaker` interface (Open/Half-Open/Closed states)
- [ ] Wrap CloudflareClientExample with retry + circuit breaker
- [ ] Wrap DiscordBridgeController HTTP calls
- [ ] Wrap LarkBridgeService HTTP calls
- [ ] Log failures and state transitions
- [ ] Unit tests with mocked HTTP (flaky network scenarios)

**Example Config:**
```json
{
  "retry": {
    "maxAttempts": 3,
    "initialDelayMs": 100,
    "backoffMultiplier": 2
  },
  "circuitBreaker": {
    "failureThreshold": 5,
    "successThreshold": 2,
    "timeoutMs": 30000
  }
}
```

---

## 🟡 MEDIUM (Backlog)

### ISSUE-009: Add Integration Tests for ECS Queries
**Severity:** MEDIUM  
**Status:** 📋 PENDING

**Description:**
EntityManager queries used throughout the codebase are not tested. Create integration tests with mocked ECS world.

**Approach:**
- Use Unity.Entities testing framework
- Create test fixtures with known entity configurations
- Test QueryRegistry caching behavior

---

### ISSUE-010: Performance Test: Zone Cleanup with 10,000+ Entities
**Severity:** MEDIUM  
**Status:** 📋 PENDING

**Description:**
CleanupZone may stall the main thread if many entities need destruction. Benchmark and optimize.

**Acceptance Criteria:**
- [ ] Create benchmark: destroy 10,000 entities in a 200m radius
- [ ] Measure frame time impact
- [ ] If > 5ms: implement chunked destruction (e.g., 100 entities/frame)
- [ ] Add profile markers for debugging

---

### ISSUE-011: Refactor Magic Numbers into Named Constants
**Severity:** MEDIUM  
**Status:** 📋 PENDING

**Description:**
Hard-coded values scattered throughout (e.g., radius=200f, maxDistance=32).

**Examples:**
- Line 671, BattleLuckPlugin.cs: `radius = 200f;`
- Multiple leash/aggro range defaults
- Cooldown durations in flow actions

**Approach:**
- Create `Constants.cs` in Core/
- Define as `public const float DefaultZoneRadius = 200f;`
- Update all usages

---

### ISSUE-012: Remove Commented-Out Debug Code
**Severity:** MEDIUM  
**Status:** 📋 PENDING

**Description:**
8+ instances of commented code block understanding; remove or move to proper feature flags.

---

### ISSUE-013: Code Coverage Reporting in CI
**Severity:** MEDIUM  
**Status:** 📋 PENDING

**Description:**
CI job should report code coverage and fail if threshold is not met.

**Approach:**
- Use OpenCover or Coverlet
- Update .github/workflows/ci.yml
- Publish coverage to Codecov

---

## 🟢 LOW (Polish)

### ISSUE-014: Security Audit of Config File Handling
**Severity:** MEDIUM  
**Status:** 📋 PENDING

**Description:**
JSON config files may contain injection vectors (e.g., malicious prefab names). Validate all user input.

**Acceptance Criteria:**
- [ ] Review all JSON deserialization in ConfigLoader.cs
- [ ] Add schema validation (optional: use JSON Schema)
- [ ] Sanitize prefab names, zone names, kit names (allow alphanumeric + underscore)
- [ ] Log parsing errors with file path/line number for debugging

---

### ISSUE-015: Add Observability/Tracing Hooks
**Severity:** LOW  
**Status:** 📋 PENDING

**Description:**
No structured logging or tracing. Add OpenTelemetry integration for better operational visibility.

**Optional Approach:**
- Activity.Start() for key operations (session start, action execution)
- Custom metrics (sessions active, players, actions/frame)
- Exporter to Application Insights or Jaeger

---

### ISSUE-016: Documentation: Deployment & Network Security
**Severity:** LOW  
**Status:** 📋 PENDING

**Description:**
Create deployment guide covering:
- Port usage (Discord, Webhook, Lark services)
- Firewall rules
- Environment variables
- SSL/TLS setup for webhooks

---

---

## Summary Table

| ID | Title | Severity | Status | Effort |
| --- | --- | --- | --- | --- |
| 001 | Revoke exposed token | 🔴 | ⚠️ ACTION | 0.25h |
| 002 | Disable AllowUnsafeBlocks | 🔴 | 📋 | 0.5h |
| 003 | Webhook signature validation | 🔴 | 📋 | 2d |
| 004 | Fix exception logging | 🔴 | 📋 | 1d |
| 005 | Unit tests (30% coverage) | 🟠 | 📋 | 2–3 sprints |
| 006 | Refactor statics → DI | 🟠 | 📋 | 4+ sprints |
| 007 | Config hot-reload validation | 🟠 | 📋 | 1d |
| 008 | Retry + circuit breaker | 🟠 | 📋 | 2d |
| 009 | ECS integration tests | 🟡 | 📋 | 1d |
| 010 | Zone cleanup perf test | 🟡 | 📋 | 0.5d |
| 011 | Magic numbers → constants | 🟡 | 📋 | 0.5d |
| 012 | Remove debug code | 🟡 | 📋 | 0.25d |
| 013 | Coverage reporting CI | 🟡 | 📋 | 0.5d |
| 014 | Config input validation | 🟡 | 📋 | 1d |
| 015 | Observability/tracing | 🟢 | 📋 | 2d |
| 016 | Deployment docs | 🟢 | 📋 | 0.5d |

---

## Quick Wins (< 1 day each)
- ✅ 001: Revoke token (user action)
- ✅ 002: Disable AllowUnsafeBlocks
- ✅ 004: Fix exception logging (grep + find/replace)
- ✅ 010: Zone cleanup perf test
- ✅ 011: Magic numbers → constants
- ✅ 012: Remove debug code

**Estimated Total for Quick Wins:** 1.5 days, high impact on security & debugging.
