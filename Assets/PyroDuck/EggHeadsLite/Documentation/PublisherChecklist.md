# Publisher follow-up

## Publish both compatible editions together

- Synchronize common code with `python Tools/AlignLiteWithFull.py --full <Full project>`.
- Preserve canonical Full script GUIDs and separate Lite content GUIDs. Never regenerate the legacy map from a later release.
- Build packages with `python Tools/BuildEditionPackages.py --full <Full project>`.
- Distribute EggHeadsLite-SharedCore and EggHeadsFull-LiteCompatible together.
- Offer EggHeadsLite-LegacyUpgrade separately so old users can prepare BEFORE importing updated code/content.
- Validate a custom project before and after Full import, plus the old-project preparation path. Back up publisher sources before syncing.

## Suggested 45-second product demo

- 0–5 seconds: Show the generated character moving in the Lite platformer.
- 5–15 seconds: Open Generator, select a preset, browse eyes and hair, change a tint.
- 15–23 seconds: Generate & Add to Scene; show the saved prefab and scene instance.
- 23–38 seconds: In a separate Full project, show the runtime creator, color wheel, save, and reload.
- 38–45 seconds: Show a concise Lite/Full comparison and the Full product page address.

Record actual behavior in the versions being distributed. This file is a recording plan, not a generated video. Publish the recording and configure its real URL only after reviewing it.

## Store consistency

Before releasing, verify the actual shipped character, part, and weapon counts in both exported packages. Align store descriptions, screenshots, README, and campaign copy. The in-editor comparison avoids unverified counts and does not claim that Lite lacks serializer code.

## Evaluate the release

Record a baseline period, then compare an equivalent period after release: Lite downloads, Full sales, support questions about setup, and questions about migration. Note discounts, traffic campaigns, and other release changes that affect comparisons. Use reporting available in your store/publisher account. The Setup page opens once per project on first import; no tracking, analytics calls or fabricated conversion figures are added.
