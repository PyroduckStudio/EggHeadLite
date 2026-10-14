"""Export both editions with Unity in an isolated publisher project.
Never opens the working project or includes unrelated asset dependencies.
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
from UnityProjectLock import is_open

parser = argparse.ArgumentParser()
parser.add_argument('--full', required=True, type=Path)
parser.add_argument('--unity', type=Path, default=Path('C:/Program Files/Unity/Hub/Editor/6000.5.8f1/Editor/Unity.exe'))
parser.add_argument('--export-project', type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
project = (args.export_project or root/'Logs/PackageExportProject').resolve()
if project == root or project == args.full.resolve(): raise SystemExit('Use an isolated export project.')
if is_open(project): raise SystemExit('Export project is open in Unity. Close it first.')
subprocess.run([sys.executable, str(root/'Tools/VerifyEditionCompatibility.py'), '--full', str(args.full)], check=True)

def ensure_metas(folder):
    for path in [folder, *folder.rglob('*')]:
        if path.suffix == '.meta': continue
        meta = path.with_name(path.name + '.meta')
        if not meta.exists():
            raise SystemExit('Missing .meta: ' + str(path))

def paths(source, folders):
    result = []
    for relative in folders:
        folder = source/relative
        ensure_metas(folder)
        result.extend(p.relative_to(source).as_posix() for p in [folder, *folder.rglob('*')] if p.suffix != '.meta')
    return sorted(result)

jobs = [
    {'paths': paths(root, ['Assets/PyroDuck/EggHeads', 'Assets/PyroDuck/EggHeadsLite', 'Assets/PyroDuck/EggHeadsUpgradeTools']), 'output': str(root/'Builds/EggHeadsLite-SharedCore.unitypackage')},
    {'paths': paths(root, ['Assets/PyroDuck/EggHeadsUpgradeTools']), 'output': str(root/'Builds/EggHeadsLite-LegacyUpgrade.unitypackage')},
    {'paths': paths(args.full, ['Assets/PyroDuck/EggHeads']), 'output': str(root/'Builds/EggHeadsFull-LiteCompatible.unitypackage')}
]
shutil.copytree(root/'Packages', project/'Packages', dirs_exist_ok=True)
(project/'ProjectSettings').mkdir(parents=True, exist_ok=True)
for name in ('ProjectVersion.txt', 'ProjectSettings.asset', 'TagManager.asset'):
    shutil.copy2(root/'ProjectSettings'/name, project/'ProjectSettings'/name)
for source, folders in [(args.full, ['EggHeads']), (root, ['EggHeads', 'EggHeadsLite', 'EggHeadsUpgradeTools'])]:
    for name in folders:
        source_folder = source/'Assets/PyroDuck'/name
        destination = project/'Assets/PyroDuck'/name
        shutil.copytree(source_folder, destination, dirs_exist_ok=True)
        shutil.copy2(source_folder.with_name(name+'.meta'), destination.with_name(name+'.meta'))
editor = project/'Assets/Editor'
editor.mkdir(parents=True, exist_ok=True)
shutil.copy2(root/'Tools/EditionPackageExporter.cs', editor/'EditionPackageExporter.cs')
(project/'edition-export.json').write_text(json.dumps({'jobs': jobs}))
(root/'Builds').mkdir(exist_ok=True)
log = project/'edition-export.log'
completed = subprocess.run([str(args.unity), '-batchmode', '-nographics', '-job-worker-count', '2', '-assetImportWorkerCount', '1', '-quit', '-projectPath', str(project), '-executeMethod', 'EditionPackageExporter.Export', '-logFile', str(log)])
if completed.returncode != 0: raise SystemExit('Unity export failed; inspect ' + str(log))
for job in jobs:
    output = Path(job['output'])
    if not output.exists(): raise SystemExit('Export missing: ' + str(output))
    with tarfile.open(output, 'r:gz') as archive:
        included = {archive.extractfile(m).read().decode().replace('\\', '/') for m in archive.getmembers() if m.name.endswith('/pathname')}
    if included != set(job['paths']):
        raise SystemExit('Export asset list mismatch: ' + output.name + '; missing=' + str(set(job['paths']) - included) + '; extra=' + str(included - set(job['paths'])))
    print(f'{output.name}: {output.stat().st_size:,} bytes')
