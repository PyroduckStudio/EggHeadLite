"""Release guard: shared code identity and independent starter content GUIDs."""
import argparse
from pathlib import Path
import re

parser = argparse.ArgumentParser()
parser.add_argument('--full', required=True, type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
shared = root/'Assets/PyroDuck/EggHeads'
full = args.full/'Assets/PyroDuck/EggHeads'

def identities(folder):
    result = {}
    for meta in folder.rglob('*.meta'):
        match = re.search(r'^guid: ([a-f0-9]{32})\s*$', meta.read_text(encoding='utf-8-sig'), re.M)
        if not match: raise SystemExit('Invalid GUID: ' + str(meta))
        identity = match.group(1)
        if identity in result: raise SystemExit('Duplicate GUID: ' + str(meta))
        result[identity] = meta
    return result

full_guids = identities(full)
lite_guids = identities(root/'Assets/PyroDuck/EggHeadsLite')
overlap = set(full_guids) & set(lite_guids)
if overlap: raise SystemExit('Lite content collides with Full: ' + str([lite_guids[g] for g in overlap]))
scripts = list(shared.rglob('*.cs'))
for source in scripts:
    relative = source.relative_to(shared)
    counterpart = full/relative
    if source.read_text(encoding='utf-8-sig') != counterpart.read_text(encoding='utf-8-sig'):
        raise SystemExit('Shared source differs: ' + str(relative))
    guid = re.search(r'^guid: (\w+)', source.with_name(source.name+'.meta').read_text(), re.M).group(1)
    target = re.search(r'^guid: (\w+)', counterpart.with_name(counterpart.name+'.meta').read_text(), re.M).group(1)
    if guid != target: raise SystemExit('Shared GUID differs: ' + str(relative))
identities(root/'Assets/PyroDuck')
for relative in ['Runtime/Scripts/InputSystem/InputSystemActions.inputactions', 'Runtime/Scripts/InputSystem/InputSystemActions.inputactions.meta']:
    if (shared/relative).read_text(encoding='utf-8-sig') != (full/relative).read_text(encoding='utf-8-sig'):
        raise SystemExit('Shared input wrapper configuration differs: ' + relative)
print(f'{len(scripts)} shared runtime/editor scripts match Full; all Lite content GUIDs are independent; no duplicate GUIDs.')
