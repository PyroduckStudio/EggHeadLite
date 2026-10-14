# Render pipeline switching

Use **Tools > PyroDuck > Render Pipeline > Make URP / Make HDRP / Make Built-in**.
Save and close untitled scenes and exit Prefab Mode before switching. This is an
Editor conversion, not a runtime graphics setting.

The tool installs the required SRP package when missing, matching the installed
SRP Core version. It resumes after package import and script compilation. Existing
packages remain installed so switching back does not lose package references.
URP is also installed when needed to resolve the original Lite material references,
including when the destination is Built-in or HDRP. It can remain installed while
Built-in is active; installing a package does not select its render pipeline.

Every switch backs up Project Settings and EggHeads prefabs/scenes under
`PyroDuckBackups/RenderPipeline`. **Restore Last Switch** restores the last successful
switch's saved files. Generated settings/materials and installed packages remain;
they can be reused on later switches. The restore restores the saved versions of
these files, including edits made since the switch, so save your later work first.

Graphics and every Quality level use the selected pipeline. The tool creates its
own pipeline assets under `Assets/PyroDuck/EggHeadsPipelineSettings`; URP uses a
2D Renderer. HDRP uses Linear color space and an unlit sprite shader supporting
sprite tint, texture alpha, flipping and sprite atlases. HDRP sprites and particles
do not gain URP 2D lighting. Custom post processing can change their appearance.
Switching back from HDRP restores the preceding color space.
HDRP sprite prefabs receive a small `EggHeadsSpritePipelineAdapter` that forwards
sprite tint and atlas textures through a reusable property block. SpriteRenderer
handles flipping. The adapter preserves other property block
values and does not create material instances. Built-in/URP conversions remove
this adapter. Runtime weapon pickups inherit their visual prefab's material and
optional adapter, so dropped weapons also use the selected pipeline.

Stock materials on EggHeads SpriteRenderers and ParticleSystemRenderers are
replaced with reusable copies. Original materials, prefab GUIDs and gameplay
scripts are preserved. Returning to the source pipeline restores original material
references. Custom materials outside EggHeads content are left unchanged.
EggHeads sample cameras receive/enable the selected pipeline's camera data; other
pipeline components stay present but disabled. User scenes and external prefabs
are not converted. Configure their lighting, cameras and materials separately.

This does not migrate an entire third-party project or guarantee compatibility
with every Custom RP. HDRP requires compatible graphics hardware and a supported
build platform. Keep a project backup and check the demo and your own scenes after
switching, especially effects, transparency sorting and UI.
