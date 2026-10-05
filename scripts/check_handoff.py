"""Check public handoff links and refresh an offline source address index on request."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]
INVENTORY = ROOT / 'docs/ai/address-inventory.json'


def source_inventory():
    files = sorted((ROOT / 'Witcher3Modifier').glob('*.cs'))
    version = (ROOT / 'Witcher3Modifier/GameVersion.cs').read_text(encoding='utf-8-sig')
    mappings = re.findall(r'\[(0x[0-9A-Fa-f]+)\]\s*=\s*(0x[0-9A-Fa-f]+)', version)
    selector = re.search(r'hash\s*==\s*"([0-9A-F]{64})"', version)
    if not selector or not mappings:
        raise ValueError('Profile selector or mappings changed; review the extractor')
    sites, direct, hashes = [], [], {}
    for path in files:
        name = path.relative_to(ROOT).as_posix()
        source = path.read_text(encoding='utf-8-sig')
        # Git may normalize CRLF and a BOM; index semantic text instead of checkout bytes.
        hashes[name] = hashlib.sha256(source.encode('utf-8')).hexdigest()
        for number, line in enumerate(source.splitlines(), 1):
            # Lexical candidates only: variable calls and encoded template operands need manual review.
            for match in re.finditer(r'(GameVersion\.Rva|ExecuteEquipmentNative|FunCheck)\((?:handle\s*,\s*module\s*,\s*)?(0x[0-9A-Fa-f]+)', line):
                sites.append({'file': name, 'line': number, 'resolver': match[1],
                              'reference_rva': match[2].upper(), 'context': line.strip()})
            for match in re.finditer(r'\b(module|baseAddress)\s*\+\s*(0x[0-9A-Fa-f]+)', line):
                direct.append({'file': name, 'line': number, 'expression': match[0],
                               'candidate_rva': match[2].upper(), 'context': line.strip()})
    return {'schema': 1, 'baseline': 'v0.1.7 runtime source',
            'source_hash_normalization': 'UTF-8 without BOM, universal-newline LF text',
            'scope': 'Lexical source index; not target-binary validation or a complete address resolver',
            'profile_selector_sha256': selector[1],
            'profile_mappings': [{'reference_rva': a.upper(), 'profile_rva': b.upper()} for a, b in mappings],
            'literal_sites': sites, 'direct_module_literals': direct, 'source_sha256': hashes}


def check_links():
    checked = 0
    documents = list((ROOT / 'docs').rglob('*.md'))
    documents += list(ROOT.glob('*.md'))
    for path in documents:
        for target in re.findall(r'!?\[[^\]]*\]\(([^)]+)\)', path.read_text(encoding='utf-8-sig')):
            if re.match(r'[a-zA-Z][a-zA-Z0-9+.-]*:', target) or target.startswith('#'):
                continue
            target = unquote(target.strip('<>').split('#', 1)[0])
            resolved = (path.parent / target).resolve()
            if not resolved.is_relative_to(ROOT) or not resolved.exists():
                raise ValueError(f'Missing/nonportable link: {path.relative_to(ROOT)} -> {target}')
            checked += 1
    for name in ['COLD_START', 'TECHNICAL_MAP', 'FAILURE_LEDGER', 'AGENT_GUIDE', 'PROJECT_STATE', 'VERSION_MIGRATION']:
        for suffix in ['.md', '.zh-CN.md']:
            if not (ROOT / 'docs/ai' / (name + suffix)).is_file():
                raise ValueError(f'Missing bilingual handoff: {name}{suffix}')
    for suffix in ['.md', '.zh-CN.md']:
        text = (ROOT / 'docs/ai' / ('FAILURE_LEDGER' + suffix)).read_text(encoding='utf-8')
        ids = re.findall(r'^## (F\d+):?', text, re.MULTILINE)
        if ids != [f'F{i:02d}' for i in range(1, 17)]:
            raise ValueError(f'Failure ledger IDs diverged: {suffix}')
    return checked


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--update-address-inventory', action='store_true')
    args = parser.parse_args()
    expected = source_inventory()
    if args.update_address_inventory:
        INVENTORY.write_text(json.dumps(expected, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    actual = json.loads(INVENTORY.read_text(encoding='utf-8'))
    if actual != expected:
        raise ValueError('Address inventory is stale. Review source changes, then use --update-address-inventory.')
    count = check_links()
    print(f'Handoff checks passed: {count} relative links, 6 bilingual guides, 16 paired failure IDs, '
          f'{len(expected["profile_mappings"])} mappings, {len(expected["literal_sites"])} literal sites, '
          f'{len(expected["direct_module_literals"])} direct-module candidates; no game access.')
