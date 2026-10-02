"""Offline invariants: translation preserves native constants and resource identities."""
import collections
import json
import re
from pathlib import Path

root=Path(__file__).resolve().parents[1]
english=root/'.build/en'
for source in (root/'Witcher3Modifier').glob('*.cs'):
    old=source.read_text(encoding='utf-8-sig')
    new=(english/'Witcher3Modifier'/source.name).read_text(encoding='utf-8-sig')
    for pattern in [r'\b0x[0-9A-Fa-f]+\b',r'Convert\.FromHexString\("([0-9A-Fa-f]+)"\)']:
        assert collections.Counter(re.findall(pattern,old))==collections.Counter(re.findall(pattern,new)),source.name
for name in ['ItemCatalog.json','EquipmentLevels.json']:
    old=json.loads((root/'Witcher3Modifier'/name).read_text(encoding='utf-8-sig'))
    new=json.loads((english/'Witcher3Modifier'/name).read_text(encoding='utf-8-sig'))
    if name=='ItemCatalog.json':
        for item in old: item['DisplayName']=item['Name']
    assert old==new,name
old=json.loads((root/'Research/equipment_presets.json').read_text(encoding='utf-8-sig'))
new=json.loads((english/'Research/equipment_presets.json').read_text(encoding='utf-8-sig'))
for group in ['Affixes','Enchantments']:
    for left,right in zip(old[group],new[group],strict=True):
        for key in left:
            if key not in ['DisplayName','Description','Equipment']: assert left[key]==right[key],(group,key)
print('Localization invariants passed: hex constants, machine code, item IDs, level data and native preset attributes unchanged.')
