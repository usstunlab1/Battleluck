#!/usr/bin/env python3
content = open(r'c:\Users\ahmad\OneDrive\Desktop\BL\docs\audit\systems\allowlists\prefabs.allowlist.txt', 'r', encoding='utf-8').read()
names = set()
for line in content.splitlines():
    line = line.strip()
    if not line:
        continue
    names.add(line)

# T08 weapon patterns
print('--- Item_Weapon_Sword_T08* ---')
for n in sorted(names):
    if n.startswith('Item_Weapon_Sword_T08'):
        print('  ', n)

print('--- Item_Weapon_Axe_T08* ---')
for n in sorted(names):
    if n.startswith('Item_Weapon_Axe_T08'):
        print('  ', n)

# Weapon tier markers (Sanguine is which tier?)
print('--- Item_Weapon_*Sanguine* ---')
for n in sorted(names):
    if 'Sanguine' in n and n.startswith('Item_Weapon_'):
        print('  ', n)

# Looking for T08 weapon variants
print('--- Item_Weapon_Any*BloodMoon* ---')
for n in sorted(names):
    if 'BloodMoon' in n and n.startswith('Item_Weapon_'):
        print('  ', n)

# Looking for Item_Chest_T08 patterns
print('--- Item_Chest_T08* (all) ---')
for n in sorted(names):
    if n.startswith('Item_Chest_T08'):
        print('  ', n)

# Looking for T06 Sanguine
print('--- Item_Chest_T06* (sample) ---')
chests = sorted([n for n in names if n.startswith('Item_Chest_T06')])
for n in chests:
    print('  ', n)
print('Total Item_Chest_T06:', len(chests))

# MagicSources: Sanguine tier usually?
print('--- Item_EquipBuff_MagicSource_T08_* ---')
for n in sorted(names):
    if n.startswith('Item_EquipBuff_MagicSource_T08_'):
        print('  ', n)
print('--- Item_MagicSource_General_T0* ---')
for n in sorted(names):
    if n.startswith('Item_MagicSource_General_T0'):
        print('  ', n)
