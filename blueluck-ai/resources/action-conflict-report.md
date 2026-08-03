# BattleLuck Action Conflict Resolution Report

**Generated**: 2026-08-02  
**Source Version**: BattleLuck 1.1.2  
**Contracts Analyzed**:
- `battleluck-actions.contract.json`
- `battleluck-commands.contract.json`
- `battleluck-events.contract.json`

---

## Executive Summary

This report identifies conflicts between the canonical action catalog, event policies, command registration, and V Rising live mod integration. It provides a prioritized fix plan with recommended canonical actions and migration paths.

### Severity Breakdown
| Severity | Count | Category |
|----------|-------|----------|
| 🔴 Critical | 9 | Circular/Duplicate Action Aliases |
| 🔴 Critical | 13 | Actions Referenced by Events But Missing from Catalog |
| 🟡 High | 1 | Command Surface Duplication |
| 🟡 High | 2 | Retired Actions Still Allowed in Events |
| 🟡 Medium | 3 | Event Policy Inconsistencies |
| 🟢 Low | 1 | Boss Data Missing Prefab Mapping |

---

## 1. Critical: Circular/Duplicate Action Aliases

These action pairs create ambiguity in routing, validation, and approval pipelines.

### 1.1 `boss.spawn` ↔ `spawn.boss`
| Property | `boss.spawn` | `spawn.boss` |
|----------|--------------|--------------|
| Category | spawn | spawn_boss |
| Required | `prefab` | `prefab` |
| Risk Level | controlled | controlled |
| Requires Approval | true | true |

**Issue**: `spawn.boss` lists `boss.spawn` as a legacyAlias, but both are canonical.

**Recommendation**:
- ✅ **Canonical**: `spawn.boss`
- ❌ **Deprecate**: `boss.spawn`
- **Migration**: Update all event configs and prompts to use `spawn.boss`

### 1.2 `blood.change` ↔ `blood.set` ↔ `set_blood`
| Property | `blood.change` | `blood.set` | `set_blood` |
|----------|----------------|-------------|-------------|
| Legacy Aliases | `["blood.set", "set_blood"]` | none | none |
| Required | none | `bloodType` | `bloodType` |

**Issue**: `blood.change` claims two legacy aliases that are also separate canonical actions with different required parameters.

**Recommendation**:
- ✅ **Canonical**: `blood.change`
- ❌ **Deprecate**: `blood.set`, `set_blood`
- **Migration**: Ensure `blood.change` accepts `bloodType` as required and `quality` as optional

### 1.3 `snapshot.restore` ↔ `snapshot.restore_old`
**Recommendation**:
- ✅ **Canonical**: `snapshot.restore`
- ❌ **Deprecate**: `snapshot.restore_old`
- **Migration**: Remove `snapshot.restore_old` from catalog

### 1.4 `snapshot.save` ↔ `snapshot.save_old`
**Recommendation**:
- ✅ **Canonical**: `snapshot.save`
- ❌ **Deprecate**: `snapshot.save_old`
- **Migration**: Remove `snapshot.save_old` from catalog

### 1.5 `mode.end` ↔ `warevent_end`
**Recommendation**:
- ✅ **Canonical**: `mode.end`
- ❌ **Deprecate**: `warevent_end`
- **Migration**: Update legacy event configs

### 1.6 `mode.start` ↔ `warevent_start`
**Recommendation**:
- ✅ **Canonical**: `mode.start`
- ❌ **Deprecate**: `warevent_start`
- **Migration**: Update legacy event configs

### 1.7 `npc.spawn` ↔ `spawn.npc`
**Recommendation**:
- ✅ **Canonical**: `npc.spawn`
- ❌ **Deprecate**: `spawn.npc`
- **Migration**: Update event configs to use `npc.spawn`

### 1.8 `disable_pvp` ↔ `pvp.disable`
**Recommendation**:
- ✅ **Canonical**: `pvp.disable`
- ❌ **Deprecate**: `disable_pvp`
- **Migration**: Update event configs and policies

### 1.9 `enable_pvp` ↔ `pvp.enable`
**Recommendation**:
- ✅ **Canonical**: `pvp.enable`
- ❌ **Deprecate**: `enable_pvp`
- **Migration**: Update event configs and policies

---

## 2. Critical: Missing Actions Referenced by Events

The following actions appear in event `allowedActions` or `blockedActions` but are **not defined** in `battleluck-actions.contract.json`.

| Missing Action | Used In | Action Needed |
|----------------|---------|---------------|
| `effect.assign` | aievent, bloodbath | Define or remove |
| `effect.remove` | aievent, bloodbath | Define or remove |
| `effect.replace` | aievent, bloodbath | Define or remove |
| `effect.status` | aievent, bloodbath | Define or remove |
| `effect.clear_group` | aievent, bloodbath | Define or remove |
| `zone.border.effect.apply` | aievent, bloodbath | Define or remove |
| `zone.border.effect.remove` | aievent, bloodbath | Define or remove |
| `zone.border.effect.replace` | aievent, bloodbath | Define or remove |
| `zone.border.effect.status` | aievent, bloodbath | Define or remove |
| `spawn.effect.assign` | aievent, bloodbath | Define or remove |
| `spawn.effect.remove` | aievent, bloodbath | Define or remove |
| `tracking.group.effect.apply` | aievent, bloodbath | Define or remove |
| `tracking.group.effect.remove` | aievent, bloodbath | Define or remove |

**Recommended Fix**:
Add these actions to the catalog with the following metadata:

```json
{
  "canonicalActionId": "effect.assign",
  "legacyAliases": [],
  "metadata": {
    "category": "effects",
    "riskLevel": "controlled",
    "requiresApproval": true,
    "required": ["effectPrefab"],
    "optional": ["target", "durationSeconds"],
    "description": "Assign a runtime visual or gameplay effect to a target."
  }
}
```

Similar definitions should be created for `effect.remove`, `effect.replace`, `effect.status`, and `effect.clear_group`.

For zone and spawn effect actions, define them as:
```json
{
  "canonicalActionId": "zone.border.effect.apply",
  "metadata": {
    "category": "zone",
    "riskLevel": "controlled",
    "requiresApproval": true,
    "required": ["zoneHash", "effectPrefab"],
    "description": "Apply a visual effect to a zone border."
  }
}
```

---

## 3. High: Command Surface Duplication

Commands are registered on both `battleluck` and `vcf` surfaces:

```
ai                 → battleluck, vcf
castlepolicy.help  → battleluck, vcf
castlepolicy.list  → battleluck, vcf
castlepolicy.private → battleluck, vcf
castlepolicy.public → battleluck, vcf
castlepolicy.remove → battleluck, vcf
castlepolicy.status → battleluck, vcf
castlepolicy.target → battleluck, vcf
castlepolicy.territory.apply → battleluck, vcf
castlepolicy.territory.preview → battleluck, vcf
clantask.cancel    → battleluck, vcf
clantask.complete  → battleluck, vcf
clantask.progress  → battleluck, vcf
```

**Recommendation**:
- Choose a single surface for command routing
- If backward compatibility is needed, mark `vcf` registrations as aliases rather than duplicate surfaces
- Update `battleluck-commands.contract.json` to reflect the canonical surface

---

## 4. High: Retired Actions Still Allowed

### 4.1 `glow.enable` / `glow.disable`
- Contract marks them as `"handlerAvailable": false`
- Retired server-side glow placeholder
- Still listed in event `allowedActions`

**Recommendation**: Remove `glow.enable` and `glow.disable` from all event `allowedActions` lists.

---

## 5. Medium: Event Policy Inconsistencies

### 5.1 `trial_of_all_actions` Uses Blocked Progression Actions
This event executes:
- `progression.unlock.all_vbloods`
- `progression.unlock.all_research`
- `progression.set_tier`

These are blocked in `aievent` and `bloodbath` policies. This is intentional for a test event, but should be explicitly documented.

### 5.2 `colosseum` Uses Undocumented `chest.spawn` Parameters
```json
{
  "type": "chest.spawn",
  "params": {
    "position": "-1750,5,-2800",
    "requiredKills": "3",
    "lootTable": "GoldIngots"
  }
}
```

The action contract only defines:
- `position` (required)
- `prefab`, `prefabGuid`, `zoneOffset`, `rotation` (optional)

**Recommendation**: Either add `requiredKills` and `lootTable` to the action contract, or remove them from the event config.

### 5.3 `bloodbath` References Unknown Techs
```json
"allowedTechs": ["sudden_death", "double_loot"]
```

These techs are not defined in `tech_catalog` contract.

**Recommendation**: Add these techs to the tech catalog or remove from event policy.

---

## 6. Low: V Rising Boss Data Missing Prefab Mapping

The frontend `vrisingData.ts` defines 31 V Blood bosses with IDs like:
- `alpha_wolf`
- `dracula_immortal_king`

But there is no mapping to game prefab names used by actions like `boss.spawn` or `spawn.boss`.

**Recommendation**: Create `boss-prefab-mapping.json` linking frontend IDs to prefab GUIDs.

---

## 7. Implementation Checklist

- [ ] Decide canonical actions for each conflict pair
- [ ] Update `battleluck-actions.contract.json` to remove deprecated aliases
- [ ] Add 13 missing effect actions to catalog
- [ ] Update event configs to use canonical action IDs
- [ ] Remove `glow.enable`/`glow.disable` from event policies
- [ ] Resolve command surface duplication
- [ ] Add `chest.spawn` parameters or remove from event
- [ ] Add `sudden_death` and `double_loot` to tech catalog
- [ ] Create `boss-prefab-mapping.json`
- [ ] Run validation script after all changes

---

## 8. Files Requiring Updates

| File | Reason |
|------|--------|
| `blueluck-ai/resources/contracts/battleluck-actions.contract.json` | Remove duplicates, add missing actions |
| `blueluck-ai/resources/contracts/battleluck-events.contract.json` | Update action references |
| `blueluck-ai/resources/contracts/battleluck-commands.contract.json` | Resolve surface duplication |
| `blueluck-ai/resources/contracts/battleluck-schemas.contract.json` | Update schemas if needed |
| `blueluck-ai/frontend/src/data/vrisingData.ts` | Add prefab references |
| `battleluck/config/BattleLuck/events/*.json` | Update live event configs |
| `battleluck/src/Services/Runtime/ActionManifestService.cs` | Update handler registry |

---

## Appendix A: Canonical Action Decision Matrix

| Function | Canonical Action | Deprecated Aliases |
|----------|------------------|-------------------|
| Spawn boss | `spawn.boss` | `boss.spawn` |
| Change blood | `blood.change` | `blood.set`, `set_blood` |
| Restore snapshot | `snapshot.restore` | `snapshot.restore_old` |
| Save snapshot | `snapshot.save` | `snapshot.save_old` |
| End event/mode | `mode.end` | `warevent_end` |
| Start event/mode | `mode.start` | `warevent_start` |
| Spawn NPC | `npc.spawn` | `spawn.npc` |
| Disable PvP | `pvp.disable` | `disable_pvp` |
| Enable PvP | `pvp.enable` | `enable_pvp` |

---

## Appendix B: Risk Assessment

If unresolved, these conflicts could cause:
- **AI operators proposing invalid actions** (missing effect actions)
- **Action approval pipeline routing to wrong handler** (duplicate aliases)
- **Event initialization failures** (retired glow actions)
- **Command conflicts between surfaces** (duplicate registrations)
- **Frontend/backend mismatch** (missing boss prefab mapping)

**Recommended Priority**: Resolve critical conflicts before next release.