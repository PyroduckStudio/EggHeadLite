# EggHeads Lite / Full architecture

Publisher workspaces: PyroDuck_EggHeadLite and sibling PyroDuck_EggHead (Full).
Current validation editor: Unity 6000.5.8f1. Sample dependencies: URP 2D, Input
System, UGUI/TMP, 2D Animation and Sprite. Required project layers are described
in the shipped Documentation/README.md.

Shared runtime scripts and assembly live in Assets/PyroDuck/EggHeads in both
editions. Lite includes common runtime and three supporting enemy classes;
Full adds creator-specific runtime and premium content. Shared source, namespaces
and script GUIDs must match. Tools/AlignLiteWithFull.py synchronizes this script
set from Full without copying premium content.

Lite content, generator, Setup and tests live in Assets/PyroDuck/EggHeadsLite.
They reference shared runtime. Lite content GUIDs differ from Full stock content
so local IDs and variant overrides survive imports. Do not copy Full prefab
.meta files onto Lite starter assets.

Assets/PyroDuck/EggHeadsUpgradeTools has a standalone Editor assembly and legacy
GUID map. Old users import it first, prepare, then import an updated edition.
Backups include old vendor files and modified custom files outside Assets.
Keep the legacy map stable across publisher releases.

Compatibility fields include damage cooldown/blood settings, direct AudioClip
settings alongside Full SoundId audio, throwable offsets and character-selection
API. Preserve script GUIDs and use serialization aliases for field renames.

Validate in an isolated project: editor suites, Platformer play smoke, custom
scene/prefab continuity after Full import, and legacy backup/rollback. Main Lite
is open in the editor; use Logs/SetupValidationProject for batch validation.

Promotion identity is configured per host in PyroDuckCampaignSettings:
currentProductId is eggheads-lite here, eggheads-full when porting to Full.
Campaign.productId identifies the advertised product, independently of campaign.id.
Both Setup and About Us use CampaignService.GetActive to exclude self promotions
across remote, cache and fallback sources. Aliases and configured product URLs
support legacy identities/feeds. PHP applies the same rule to the product query;
products audience targeting is separate. Never infer edition from shared runtime.

Full Generator's presentation lives in EggHeadsGeneratorUI.cs (partial of the
original EggHeadsCharacterPrefabGenerator) and EggHeadsGenerator.uss. It shares
the Lite card/gallery style while retaining original Full generation/configuration,
automatic database registration and PreviewRenderUtility rendering. UI changes
record Undo and refresh fields after undo; keep serialized field names and script
GUID stable. FullGeneratorUITests exercise UI events through saved prefab output.
