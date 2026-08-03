#!/usr/bin/env node
/**
 * Apply Phase 1: Action Catalog Reconciliation
 * Modifies blueluck-ai/resources/contracts/battleluck-actions.contract.json
 * - Removes 11 deprecated duplicate actions (C-1)
 * - Adds 13 missing effect actions (C-2)
 * - Extends chest.spawn params (C-4)
 * - Resolves notify/notification/send_message (C-5)
 * - Adds heal aliases (C-6)
 * - Regenerates recordsJson + contentHash (C-14 partial)
 */
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const contractPath = path.resolve(__dirname, '..', '..', 'blueluck-ai', 'resources', 'contracts', 'battleluck-actions.contract.json');

if (!fs.existsSync(contractPath)) {
  console.error('Contract not found:', contractPath);
  process.exit(1);
}

const raw = fs.readFileSync(contractPath, 'utf8');
const doc = JSON.parse(raw);

// Parse records from recordsJson
const records = JSON.parse(doc.recordsJson);
if (!Array.isArray(records)) {
  console.error('recordsJson is not an array');
  process.exit(1);
}

// C-1: Remove 11 deprecated actions
const deprecated = new Set([
  'boss.spawn', 'blood.set', 'set_blood', 'snapshot.restore_old', 'snapshot.save_old',
  'warevent_end', 'warevent_start', 'spawn.npc', 'disable_pvp', 'enable_pvp', 'spawnwave'
]);
const removed = [];
const afterRemoval = records.filter(r => {
  if (deprecated.has(r.canonicalActionId)) { removed.push(r.canonicalActionId); return false; }
  return true;
});
console.log('Removed deprecated actions:', removed.join(', '));

// C-5: Resolve notify/notification/send_message
afterRemoval.forEach(r => {
  if (r.canonicalActionId === 'notify') {
    r.legacyAliases = ['notify'];
  }
  if (r.canonicalActionId === 'send_message') {
    r.legacyAliases = ['send_message'];
  }
  if (r.canonicalActionId === 'notification') {
    const existing = Array.isArray(r.legacyAliases) ? r.legacyAliases : [];
    r.legacyAliases = Array.from(new Set([...existing, 'notify', 'send_message']));
    if (!r.metadata.aliases) r.metadata.aliases = [];
    r.metadata.aliases = Array.from(new Set([...r.metadata.aliases, 'message']));
  }
});
console.log('C-5: notify/send_message now legacyAliases of notification');

// C-6: Add heal aliases
afterRemoval.forEach(r => {
  if (r.canonicalActionId === 'heal') {
    if (!r.metadata.aliases) r.metadata.aliases = [];
    r.metadata.aliases = Array.from(new Set([...r.metadata.aliases, 'full health', 'restore health']));
  }
});
console.log('C-6: heal aliases added');

// C-4: Extend chest.spawn params
afterRemoval.forEach(r => {
  if (r.canonicalActionId === 'chest.spawn') {
    if (!r.metadata.optional) r.metadata.optional = [];
    r.metadata.optional = Array.from(new Set([...r.metadata.optional, 'requiredKills', 'lootTable']));
  }
});
console.log('C-4: chest.spawn params extended');

// C-2: Add 13 effect actions
const effectActions = [
  { id: 'effect.assign', category: 'effects', requireApproval: true, required: ['effectPrefab'], optional: ['target', 'durationSeconds'], desc: 'Assign a runtime visual or gameplay effect to a target.' },
  { id: 'effect.remove', category: 'effects', requireApproval: false, required: ['effectPrefab'], optional: ['target'], desc: 'Remove a runtime visual or gameplay effect from a target.' },
  { id: 'effect.replace', category: 'effects', requireApproval: true, required: ['oldPrefab', 'newPrefab'], optional: ['target'], desc: 'Replace one runtime effect with another.' },
  { id: 'effect.status', category: 'effects', requireApproval: false, required: [], optional: ['target'], desc: 'Query current runtime effect assignments.' },
  { id: 'effect.clear_group', category: 'effects', requireApproval: true, required: [], optional: ['group'], desc: 'Clear all runtime effects for a tracking group.' },
  { id: 'zone.border.effect.apply', category: 'zone', requireApproval: true, required: ['zoneHash', 'effectPrefab'], optional: [], desc: 'Apply a visual effect to a zone border.' },
  { id: 'zone.border.effect.remove', category: 'zone', requireApproval: false, required: ['zoneHash'], optional: ['effectPrefab'], desc: 'Remove a visual effect from a zone border.' },
  { id: 'zone.border.effect.replace', category: 'zone', requireApproval: true, required: ['zoneHash', 'oldPrefab', 'newPrefab'], optional: [], desc: 'Replace a zone border visual effect.' },
  { id: 'zone.border.effect.status', category: 'zone', requireApproval: false, required: ['zoneHash'], optional: [], desc: 'Query zone border effects.' },
  { id: 'spawn.effect.assign', category: 'effects', requireApproval: true, required: ['effectPrefab'], optional: ['spawnGroup'], desc: 'Assign a runtime effect to a spawned group.' },
  { id: 'spawn.effect.remove', category: 'effects', requireApproval: false, required: ['effectPrefab'], optional: ['spawnGroup'], desc: 'Remove a runtime effect from a spawned group.' },
  { id: 'tracking.group.effect.apply', category: 'effects', requireApproval: true, required: ['trackingGroup', 'effectPrefab'], optional: [], desc: 'Apply an effect to all entities in a tracking group.' },
  { id: 'tracking.group.effect.remove', category: 'effects', requireApproval: false, required: ['trackingGroup', 'effectPrefab'], optional: [], desc: 'Remove an effect from all entities in a tracking group.' }
];

const existingIds = new Set(afterRemoval.map(r => r.canonicalActionId));
const added = [];
for (const ea of effectActions) {
  if (existingIds.has(ea.id)) continue;
  afterRemoval.push({
    canonicalActionId: ea.id,
    legacyAliases: [],
    metadata: {
      category: ea.category,
      riskLevel: 'controlled',
      requiresApproval: ea.requireApproval,
      aliases: [],
      required: ea.required,
      optional: ea.optional,
      description: ea.desc,
      handlerAvailable: true
    }
  });
  added.push(ea.id);
}
console.log('Added effect actions:', added.join(', '));

// Serialize records back to compact JSON (matching original formatter)
const newRecordsJson = JSON.stringify(afterRemoval);

// Recompute contentHash = SHA256(recordsJson) lowercase hex
const newHash = crypto.createHash('sha256').update(newRecordsJson, 'utf8').digest('hex');

// Update the document
doc.recordsJson = newRecordsJson;
doc.contentHash = newHash;
doc.sourceHash = newHash;

// Write back with 2-space indent (matching original file format)
fs.writeFileSync(contractPath, JSON.stringify(doc, null, 2) + '\n', 'utf8');

console.log('Updated:', contractPath);
console.log('New contentHash:', newHash);
console.log('Records count:', afterRemoval.length);
console.log('Phase 1 (C-1,C-2,C-4,C-5,C-6) applied. Run validate-contracts.ps1 to verify.');