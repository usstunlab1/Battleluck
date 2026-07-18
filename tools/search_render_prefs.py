#!/usr/bin/env python3
"""Search render-prefabs.json for valid V Rising prefab names matching patterns."""
import json
import re
import sys

with open(r'c:\Users\ahmad\OneDrive\Desktop\BL\Data\render-prefabs.json', 'r') as f:
    data = json.load(f)

# Get all prefab names - it's a dict mapping name -> entry
prefabs = data.get('prefabs', {})
print(f"Total prefabs: {len(prefabs)}")

# Show first few entries
keys = list(prefabs.keys())[:3]
for k in keys:
    print(f"{k}: {prefabs[k]}")
