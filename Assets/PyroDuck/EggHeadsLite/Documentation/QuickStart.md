# Create your first EggHead

On first import, **EggHeads Lite Setup** opens once per project after Unity finishes
compiling and importing. It provides Generator, demo and guide shortcuts plus
the current Full campaign. Reopen it from **Tools > PyroDuck > EggHeadsLite > Setup**.
The page does not change project settings automatically.

1. Open **Tools > PyroDuck > EggHeadsLite > Generator**.
2. Under Quick Start, select a preset, or click **Load Defaults** below the character fields.
3. Expand **Browse Eyes**, **Browse Hair / Hat**, or another category. Select a thumbnail; use Search to filter. The prefab fields still accept your own assets.
4. Change Tint where supported. Choose **None** to remove an optional part.
5. Enter a prefab name and output folder. **Generate** saves the prefab. **Generate & Add to Scene** also places an instance at the Scene view center (Z = 0).
6. Use **Open Playable Demo**, then press Play to try the included platformer. Unity asks you to save modified scenes before switching.

The preview is isolated from your scene. Generate does not add temporary characters to your active scene. Adding an instance supports Undo; Undo does not delete the saved prefab. Automatic database registration is undoable separately.

## Undo and defaults

Undo/Redo restores part selections, colors, presets, and output settings. Load Defaults resets the character parts and tints; your output folder, name, and database remain selected. The default eyebrows are EyeBrow_0 Variant and facial hair is Mustache_0 Variant.

## Sample setup

Check Setup reports missing references and common sample configuration problems without modifying your project settings. The editor generator is separate from the optional playable sample.

The sample requires compatible Input System, 2D Animation, 2D Sprite, UGUI/TMP, and URP packages. Use Unity Package Manager for versions compatible with your editor. For Unity 2022.3 install TextMesh Pro; on Unity 6 it ships with UGUI. Import TMP Essential Resources when needed. Set Active Input Handling to Input System Package (New) or Both and restart when Unity requests it.

The supplied sample prefabs use these layer indices: Ground=3, Throwable=6, Character=7, SecondGround=8, Mele=9, Bullet=10, Spike=11, Enemy=12. In an existing project, do not overwrite occupied layers: remap the sample's layers and collision masks deliberately in a copy of the project. The original sample materials use URP 2D. To configure Built-in, URP 2D or HDRP, use **Tools > PyroDuck > Render Pipeline**; see [Render pipeline switching](RenderPipelines.md) for scope and backup behavior.

The Platformer scene already includes AudioManager and ProjectilePool. When bringing gameplay prefabs into your own scene, provide these systems as described in README.md. The character generator alone does not configure your gameplay scene.

## Full edition

Expand Explore Egg Heads Full for the product page and migration guide. The product page contains the publisher's product media; the playable demo button opens the local Lite sample. No separate hosted video is configured by this update.
