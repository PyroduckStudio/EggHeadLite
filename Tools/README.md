# EggHeads edition publishing

Run from the Lite project with Python and an installed Unity editor:

```powershell
python Tools/AlignLiteWithFull.py --full "D:\WorkStation\Projects\PyroDuck\EggHead\PyroDuck_EggHead"
python Tools/VerifyEditionCompatibility.py --full "D:\WorkStation\Projects\PyroDuck\EggHead\PyroDuck_EggHead"
python Tools/BuildEditionPackages.py --full "D:\WorkStation\Projects\PyroDuck\EggHead\PyroDuck_EggHead"
```

Build uses Unity 6000.5.8f1 by default; override its executable with `--unity`.
It creates an isolated project at Logs/PackageExportProject. A closed cached
validation project can be supplied with `--export-project`; never use your
working Lite/Full project. The first export can take longer for package/asset
imports. The Unity log is edition-export.log within the export project.

Output is three native unitypackages in Builds. Export lists are explicit so
Lite does not include Full premium content even though the isolated export
project temporarily contains both editions. Dependencies and project settings
are excluded. Exact exported paths are checked against the release lists.

Publish SharedCore Lite and LiteCompatible Full together. Old users receive the
standalone LegacyUpgrade package first. Keep LegacyLiteMap.json stable: it maps
original released Lite GUIDs to the permanent shared-core identities. Never
regenerate it from a newer baseline or regenerate existing starter .meta files.

Synchronizing replaces shared source. Commit/back up publisher modifications
first. New shared scripts must use the same .meta GUID in both editions. Lite
starter content must have independent GUIDs. Shared input action metadata must
generate its wrapper in the Full namespace.
