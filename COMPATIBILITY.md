# EggHeads Lite compatibility verification

## Full Generator UI

Full's Generator now uses Lite's UI Toolkit card styles and galleries. The existing
generation, database, configuration/material and preview render method bodies were
verified unchanged against Logs/full-generator-before-style.cs. All 31 Full Editor
tests passed on Unity 6000.6.4f1 with Direct3D 11, including new UI event-to-prefab
color/part persistence, database registration, window recreation, Undo and weapon
gallery coverage. Evidence: Logs/full-generator-ui-tests.xml and its matching log.

## Promotion product identity

Full now contains its own promotion module under EggHeads/Editor/PyroDuck/AboutUs,
with independent asset GUIDs, namespace and menu/preference keys. Its host identity
is eggheads-full and its own Asset Store URL also suppresses legacy self ads.
41/41 Full editor and Lite campaign/setup tests passed with both editions installed
on Unity 6000.6.4f1. Tests cover Full-only assembly dependencies, own/other products,
both windows and existing Full functionality. Evidence: Logs/full-promotion-tests.xml
and Logs/full-promotion-tests.log.

Self-promotion filtering passed 14/14 targeted CampaignTests and SetupWindowTests
on Unity 6000.6.4f1 (isolated validation project). This covers Lite/Full identities,
aliases, legacy product URLs, and hidden self cards in both windows. The PHP API
passed 32 integration checks, including product identity serialization, rejection
of invalid identity types, and host-product suppression. Evidence:
`Logs/promotion-identity-tests.xml` and `Logs/promotion-identity-tests.log`.
Unity 6000.5.8f1 could not start a new validation instance because its installed
Editor/Data resources were missing; the open working editor was left running.

## Current shared-core upgrade

The current release uses Full's runtime assembly, namespace and script GUIDs
under `Assets/PyroDuck/EggHeads`. Lite content stays independently identified
under `Assets/PyroDuck/EggHeadsLite`. Both editions must be released together.
The historic verification below describes earlier Lite-only releases.

Shared-core validation on Unity 6000.5.8f1 passed 44/44 Lite tests initially,
then 68/68 tests with Full content installed (including the new pool regression
and Full suites). A custom prefab/variant, database and scene retained their
GUIDs, local IDs, dependencies, health/cooldown values and scene overrides.
Older Unity versions have not yet been rerun for this architectural change.

Final exported-source verification passed 67/67 editor tests and the separate
Platformer play startup test (1/1) on Unity 6000.5.8f1. The play test used the
Direct3D 11 backend after headless GUI reload and memory limitations on this
machine. The three exported packages' C# payloads match the current source.
Final evidence: Logs/final-editor-tests.xml, Logs/legacy-full-tests.xml,
Logs/legacy-tests.log and Logs/verify-final-release.py.

Use `Builds/EggHeadsLite-SharedCore.unitypackage` for new projects and
`Builds/EggHeadsFull-LiteCompatible.unitypackage` for Full upgrades. Older Lite
projects must first import `Builds/EggHeadsLite-LegacyUpgrade.unitypackage` and
prepare through Tools > PyroDuck > EggHeadsLite > Prepare Legacy Project for Full.
See the shipped Documentation/UpgradeToFull.md for backup and restore steps.

Publisher guards: Tools/VerifyEditionCompatibility.py and
Tools/AlignLiteWithFull.py. Export with Tools/BuildEditionPackages.py; it uses
Unity's native exporter in an isolated project and verifies exact asset lists.
Do not distribute earlier EggHeadsLite / WithSetup exports for this upgrade.

Current evidence: Logs/shared-core-tests.xml, Logs/full-import-tests.xml and
Logs/continuity-verify.log. Rendering/interactive behavior was not visually
reviewed; tests verify references, editor workflows and sample play startup.

The original Lite baseline was also restored into the isolated project. A
custom prefab/variant, database and scene were created with the old runtime.
The standalone LegacyUpgrade package was imported, preparation created a backup
and moved/referenced the shared core, and Full was imported. Continuity checks
passed with no missing scripts and unchanged custom GUIDs/local IDs, dependency
paths, health/cooldown tuning and scene overrides. Evidence:
Logs/legacy-import-tools.log, Logs/legacy-prepare.log, Logs/legacy-verify.log.

Existing work was saved in commit `4976842` before this migration.

## Results

| Unity editor | Tests passed | Platformer Play Mode smoke test |
| --- | --- | --- |
| 6000.5.8f1 | 19/19 | Passed |
| 6000.3.22f1 | 19/19 | Passed |
| 2022.3.62f3 | 19/19 | Passed |

Tests cover the existing event, serialization and health behavior, stable object
identifiers, and 120 frames of the Platformer scene with a live character visual.
The 2022.3 and 6000.3 runs used separate projects containing only the distributed
Lite assets, the required packages, and the documented project layers. TMP
Essential Resources were installed automatically in both clean projects.

The exact requested 2022.3.23 editor was not installed. 2022.3.62f3 is the tested
2022.3 LTS version; this is not an exact-patch verification of 2022.3.23.
These are headless tests; rendering and interactive controls were not visually inspected.

## Distribution

Import `Builds/EggHeadsLite.unitypackage`. The package lives entirely under
`Assets/PyroDuck/EggHeadsLite`; no local UPM registration is required.
The three existing ready-made characters were moved from
`Assets/EggHeadsLite/GeneratedHeads` into `Runtime/Prefabs/Characters` with their
GUIDs preserved. Otherwise they were absent from clean imports.
Existing asset GUIDs and Lite content limits were preserved.

Full's object-ID compatibility, input cleanup, jump handling, TMP installation,
and HP-bar cleanup were ported. Lite's damage cooldown, weapon/pool APIs and
character-selection workflow remain in place. The stale Full demo entry in
EditorBuildSettings now points to Lite's Platformer scene.

ProjectSettings and Package Manager packages are not bundled. See the shipped
Documentation/README.md for dependencies, input handling and layer indices.
The only asset reference outside Lite is TMP's standard LiberationSans SDF,
provided by TMP Essential Resources.

Test results: `Logs/final-<Unity version>.xml`.
Export log: `Logs/export-lite-final.log`.

## Editor campaigns update

The About Us/campaign update passed 29/29 tests on each of 6000.5.8f1,
6000.3.22f1 and 2022.3.62f3, including the existing Platformer smoke test.
Results are in `Logs/campaign-final-<Unity version>.xml`.
The additional tests cover UXML/USS loading, fallback service disposal, filtering,
JSON schema, and permitted destination URLs. The feed is intentionally unconfigured;
live HTTPS/image delivery has not been tested against a production host.
No visual screen inspection was performed.

## Generator usability verification

Unity 6000.5.8f1 EditMode: 39/39 tests passed in an isolated copy of the project.
Results: Logs/workflow-tests-verified.xml. New coverage includes all weapon subgroups,
preset loading/exportable appearance, reset/Undo, persistent None selection, isolated
prefab generation, database registration, source prefab preservation, undoable scene
placement, and repeated UI creation. Existing 29 tests also passed.
The new UI was not visually inspected in a running editor. This update has not yet
been rerun on Unity 2022.3 or 6000.3; previous version results above apply to the
earlier package state.
