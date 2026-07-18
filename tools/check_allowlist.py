#!/usr/bin/env python3
import os

content = open(r'c:\Users\ahmad\OneDrive\Desktop\BL\docs\audit\systems\allowlists\prefabs.allowlist.txt', 'r', encoding='utf-8').read()
names = set()
for line in content.splitlines():
    line = line.strip()
    if not line:
        continue
    names.add(line)
print('Total entries:', len(names))

broken = [
    'Item_Weapon_Sword_T08_Sanguine',
    'Item_Weapon_Axe_T08_Sanguine',
    'Item_Weapon_Crossbow_T08_Sanguine',
    'Item_Chest_T08_DarkSilver_Brute',
    'Item_Chest_T08_DarkSilver_Scholar',
    'Item_Chest_T08_DarkSilver_Rogue',
    'Item_Chest_T08_DarkSilver_Warrior',
    'Item_Chest_T08_DarkSilver',
    'Item_MagicSource_General_T08_Chaos',
    'Item_MagicSource_General_T08_Frost',
    'Item_MagicSource_General_T08_Storm',
    'Item_MagicSource_General_T08_Blood',
]
print('--- Broken names from kits.json ---')
for b in broken:
    status = 'IN_ALLOWLIST' if b in names else 'MISSING'
    print('  ', b, ':', status)

print()
print('--- T08 entries in allowlist (first 30) ---')
t08 = sorted([n for n in names if 'T08' in n])
for n in t08[:30]:
    print('  ', n)
print('Total T08:', len(t08))

print()
print('--- T05 entries in allowlist (first 30) ---')
t05 = sorted([n for n in names if 'T05' in n])
for n in t05[:30]:
    print('  ', n)
print('Total T05:', len(t05))

print()
print('--- T06 entries in allowlist (first 30) ---')
t06 = sorted([n for n in names if 'T06' in n])
for n in t06[:30]:
    print('  ', n)
print('Total T06:', len(t06))

print()
print('--- MagicSource entries (first 30) ---')
ms = sorted([n for n in names if 'MagicSource' in n])
for n in ms[:30]:
    print('  ', n)
print('Total MagicSource:', len(ms))
