"""Generate the English source tree without changing the Chinese source or native IDs."""
import argparse
import json
import re
import shutil
from pathlib import Path

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, default=root / '.build' / 'en')
args = parser.parse_args()
target = args.output.resolve()
if root not in target.parents or target == root:
    parser.error('Generated sources must be in a child directory of this repository')
translations = json.loads((root / 'localization/en.json').read_text(encoding='utf-8'))
pattern = re.compile('|'.join(re.escape(k) for k in sorted(translations, key=len, reverse=True)))

def translate(text):
    return pattern.sub(lambda match: translations[match.group()], text)

for folder in ['Witcher3Modifier', 'DesignPreview', 'Research', 'tools']:
    shutil.copytree(root / folder, target / folder, dirs_exist_ok=True,
                    ignore=shutil.ignore_patterns('bin', 'obj', 'draft.json', '*.log.jsonl'))
source_files = list((target / 'Witcher3Modifier').glob('*.cs')) + [p for p in (target / 'tools').rglob('*.cs') if 'obj' not in p.parts and 'bin' not in p.parts]
for path in source_files:
    source = path.read_text(encoding='utf-8-sig')
    # Chinese aliases remain searchable in both language builds.
    aliases = {name: f'ALIAS_ZH_{i}' for i, name in enumerate(['毒蛇学派', '毒蛇派', '蛇派'])}
    for name, marker in aliases.items():
        source = source.replace('"' + name + '"', '"' + marker + '"')
    source = translate(source).replace('Microsoft YaHei UI', 'Segoe UI')
    for name, marker in aliases.items():
        source = source.replace('"' + marker + '"', '"' + name + '"')
    if path.name == 'MainForm.Theme.cs':
        source = source.replace('SetToolTip(close,"Off")', 'SetToolTip(close,"Close")')
    path.write_text(source, encoding='utf-8')

catalog_path = target / 'Witcher3Modifier/ItemCatalog.json'
catalog = json.loads(catalog_path.read_text(encoding='utf-8-sig'))
for item in catalog:
    item['DisplayName'] = item['Name']
catalog_path.write_text(json.dumps(catalog, ensure_ascii=False), encoding='utf-8')

descriptions = [
    'After casting a Sign, spend 1 Adrenaline point to imbue the next sword strike with that Sign.',
    'Increases the range of Whirl and Rend.',
    'At full health, healing increases the damage of the next attack.',
    'Grindstone and armorer-table bonuses remain active.',
    'Food restores more health.',
    'At maximum Adrenaline, increases regeneration and reduces toxicity more quickly.',
    'A fatal sword strike restores stamina.',
    'Unblocked sword hits extend active potion duration.',
    'Fatal sword strikes grant Adrenaline.',
    'Deflects incoming arrows.',
    'All equipped armor is treated as light armor.',
    'All equipped armor is treated as medium armor.',
    'All equipped armor is treated as heavy armor.',
    'Chance to reflect part of the damage received.',
    'Aard reduces the stamina of enemies hit.',
    'Basic Igni strikes all around the player, but does not cause burning.',
    'When an Axii-affected enemy dies, the effect transfers to a nearby target.',
    'Enemies burning from Igni may ignite nearby enemies.',
    'Hits against an Axii-affected enemy extend the Sign duration.',
    'A hit from the alternate Yrden trap creates a Yrden glyph.',
    'Chance to activate Quen without a stamina cost at the start of combat.',
    'Axii can transfer on death; hits extend its duration.',
    'Enemies killed while burning from Igni explode and ignite nearby enemies.',
]
preset_path = target / 'Research/equipment_presets.json'
presets = json.loads(preset_path.read_text(encoding='utf-8-sig'))
for group in ['Affixes', 'Enchantments']:
    for item in presets[group]:
        item['DisplayName'] = translate(item['DisplayName'])
        if re.search('[\u4e00-\u9fff]', item['DisplayName']):
            item['DisplayName'] = item['Id']
        if 'Equipment' in item:
            item['Equipment'] = translate(item['Equipment'])
affix_names = ['Slashing resistance','Piercing resistance','Bludgeoning resistance',
    'Monster damage resistance','Elemental resistance','Poison resistance','Bleeding resistance',
    'Burning resistance','Vitality','Adrenaline gain','Attack power (%)','Attack power',
    'Stamina regeneration','Armor piercing','Critical hit chance','Critical hit damage',
    'Infinite durability','Aard intensity','Igni intensity','Quen intensity','Yrden intensity',
    'Axii intensity','Gold reward bonus','Monster kill XP','Human kill XP','Bleeding chance',
    'Freezing chance','Poison chance','Burning chance','Stagger chance']
enchantment_names = ['Replenishment','Severance','Invigoration','Preservation','Dumplings',
    'Placation','Rejuvenation','Prolongation','Elation','Deflection','Levity','Balance','Heft',
    'Retribution','Depletion','Rotation','Usurpation','Ignition','Beguilement','Entanglement',
    'Protection','Possession','Eruption']
for item, name in zip(presets['Affixes'], affix_names, strict=True): item['DisplayName'] = name
for item, name in zip(presets['Enchantments'], enchantment_names, strict=True): item['DisplayName'] = name
for item, description in zip(presets['Enchantments'], descriptions, strict=True):
    item['Description'] = description
preset_path.write_text(json.dumps(presets, ensure_ascii=False), encoding='utf-8')
print(f'English sources generated: {target}')
