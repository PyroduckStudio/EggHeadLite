# Moving from Lite to Full

Lite is a usable editor character generator with a starter collection and playable sample. Full adds a larger content collection and a ready-made runtime creator with color wheel and integrated appearance save/load. Lite already contains serializer source code: the distinction is the supplied complete runtime workflow, not exclusive ownership of JSON serialization.

## Continue in the same project

1. Back up or commit your project, generated prefabs, scenes, and custom parts.
2. If your project uses `Assets/PyroDuck/EggHeads/Runtime/com.pyroduck.eggheads.Runtime.asmdef`, import the compatible Full package into the same project. Include its shared Runtime and Editor code.
3. Let Unity finish importing and compiling. Open your existing scene and continue using your existing characters. Register additional Full parts in your own databases when needed.
4. Test character selection, damage, weapons, audio and save/load before continuing development.

Both editions use the same runtime namespace, assembly and script GUIDs under `Assets/PyroDuck/EggHeads`. Full updates those scripts and adds its content. Lite's starter assets, database, generator, Setup page and samples remain under `Assets/PyroDuck/EggHeadsLite` with separate GUIDs. Custom prefabs keep their GUIDs, serialized tuning and local file IDs. Use the compatible Full release containing Lite's legacy fields and helpers; an older Full release is not a supported replacement.

## Older Lite projects: prepare before importing either updated edition

Older Lite used `com.pyroduck.eggheadslite.Runtime` and starter GUIDs that collided with Full. Importing Full first could overwrite assets and lose overrides.

1. Back up your complete project and save open scenes. Use text serialization for scenes, prefabs and ScriptableObjects.
2. Import **EggHeadsLite-LegacyUpgrade.unitypackage** only. It has no runtime dependency.
3. Open **Tools > PyroDuck > EggHeadsLite > Prepare Legacy Project for Full**.
4. Select **Analyze Project**, review the report, then **Back Up and Prepare Project**.
5. The tool rewrites old Lite GUID references throughout Assets, updates runtime namespace/assembly references, and moves shared source. Local IDs and custom asset GUIDs remain intact. A backup with restoration instructions is created under `EggHeadsUpgradeBackups` outside Assets.
6. Import updated Lite or compatible Full and test your existing scenes.

Binary scenes/prefabs and binary assets depending on Lite are rejected. Re-save them as text and analyze again. If Full was already imported, restore the pre-import backup first. Merge custom modifications to vendor source before importing; package import replaces vendor scripts. External compiled assemblies and source outside Assets need their runtime assembly references updated separately.

Appearance JSON export remains useful for sharing looks, but this upgrade workflow does not require recreating characters from JSON. JSON includes part names and colors. Custom save formats, renamed parts and source modifications still need application-specific verification.

## Product comparison

| Workflow | Lite | Full |
| --- | --- | --- |
| Editor character prefabs | Included, with galleries and scene placement | Included |
| Starter playable example | Included | Included |
| Character collection | Starter selection | Expanded selection |
| Ready-made runtime creator with color wheel | Not supplied as the supported Lite workflow | Included |
| Integrated runtime appearance save/load UI | Not supplied as the supported Lite workflow | Included |

Use the current product page for released content and compatibility details. Exact counts are intentionally omitted here so local development additions are not advertised as shipped content.
