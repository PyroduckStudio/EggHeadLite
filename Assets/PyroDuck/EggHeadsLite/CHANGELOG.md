# Changelog

All notable changes to EggHeads Lite are documented in this file.

## [1.0.1] - 2026-10-10

### Added
- Optional Built-in, URP 2D, and HDRP render pipeline conversion tools with backup and restore. HDRP conversion uses unlit sprite rendering.
- Setup and About Us windows with quick access to tools, samples, and documentation.
- Upgrade compatibility with EggHeads Full, preserving existing asset references and supported legacy settings.
- Documentation for Lite-to-Full upgrades and render pipeline setup.

### Improved
- Redesigned the Character Generator with searchable part galleries, a live preview, and Undo/Redo support.
- Added pipeline-aware Generator previews and HDRP sprite tint support.
- Re-exported audio with extra headroom and imported the clips as PCM.
- Switched the Platformer sample particle material to the URP Particles/Unlit shader.

### Fixed
- Fixed audio files that peaked above the Asset Store -0.3 dB limit.
- Fast Enter Play Mode no longer keeps stale static state or event subscribers between sessions.

## [1.0.0] - 2026-05-18

### Added
- Initial release.
- Editor prefab generator at Tools > PyroDuck > EggHeadsLite > Generator.
- 3 ready-to-play characters: RangedMan, PunkMan, and BomberMan.
- Three weapon categories (ranged, melee, throwable), one prefab each.
- Event-driven audio system with a pre-configured AudioLibrary asset.
- Optional platformer sample scene with audio wired up.
- EggHeadDatabaseSO with a Validate Database inspector tool.
