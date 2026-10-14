"""Publisher tool: synchronize the shared core using Full's canonical GUIDs.
Never copies Full creator UI, premium content, scenes, prefabs or project settings.
"""
from pathlib import Path
import argparse
import re

parser = argparse.ArgumentParser()
parser.add_argument('--full', required=True, type=Path)
args = parser.parse_args()
lite = Path(__file__).resolve().parents[1] / 'Assets/PyroDuck/EggHeads'
full = args.full / 'Assets/PyroDuck/EggHeads'
scripts = lite / 'Runtime/Scripts'
full_scripts = full / 'Runtime/Scripts'
if not full_scripts.is_dir():
    raise SystemExit('Full runtime scripts not found')
support = ['Character/EnemyAnimationsController.cs', 'Combat/EnemyActionChecker.cs', 'Combat/EnemyController.cs']
paths = sorted({p.relative_to(scripts).as_posix() for p in scripts.rglob('*.cs')} | set(support))
mapping = []
def guid(path):
    return re.search(r'^guid: ([a-f0-9]{32})$', path.with_name(path.name + '.meta').read_text(), re.M).group(1)
for relative in paths:
    source = full_scripts / relative
    destination = scripts / relative
    if not source.is_file():
        raise SystemExit('Missing compatible Full source: ' + relative)
    destination.parent.mkdir(parents=True, exist_ok=True)
    text = source.read_text(encoding='utf-8-sig')
    destination.write_text(text, encoding='utf-8')
    meta = destination.with_name(destination.name + '.meta')
    if not meta.exists():
        meta.write_bytes(source.with_name(source.name + '.meta').read_bytes())
    if guid(destination) != guid(source):
        raise SystemExit('Script GUID mismatch: ' + relative)
    mapping.append({'relativePath': relative, 'liteGuid': guid(destination), 'fullGuid': guid(source)})
print(f'Aligned {len(mapping)} runtime scripts; both editions use identical GUIDs and namespaces.')
for relative in ['InputSystem/InputSystemActions.inputactions', 'InputSystem/InputSystemActions.inputactions.meta']:
    (scripts/relative).write_bytes((full_scripts/relative).read_bytes())
print('Aligned input action asset and wrapper generation namespace.')
