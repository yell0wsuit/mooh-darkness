# JSON Assets Implementation Plan

**Goal:** Replace legacy runtime asset metadata with editable JSON and bitmap text with bundled Comic Neue.

**Architecture:** Keep presentation models and tracker replay behavior, replacing only their readers and shipped metadata. Adapt Contre Jour DX's TexturePacker Array/Hash, trim and pivot conventions to Mooh's named frames; use its cached Skia typeface/font approach for text measurement and drawing. JSON is preferred to hardcoded audio definitions because the data remains editable and consistent with the other migrations. No dual legacy runtime loaders are needed.

**Tech Stack:** C#/.NET 10, System.Text.Json DOM (AOT safe), SkiaSharp, Python conversion and packaging tools, xUnit.

## Separate checkpoints

1. **TexturePacker atlases:** Add JSON contract tests and observe failure. Convert every sprite SEQ without changing pixels or names; preserve signed anchor offsets, including empty frames. Replace SpritePage reader, renderer/mask paths, project content and browser fixtures. Run presentation and desktop regressions, then commit.
2. **Map waypoints:** Add JSON ordering/malformed-data tests and observe failure. Convert all 151 coordinates to an ordered JSON array, replace reader and paths, run map tests, then commit.
3. **Strings:** Add Unicode JSON tests and observe failure. Convert the language and all 457 entries losslessly to UTF-8 JSON; replace reader and paths, retain encoding helper only until bitmap fonts are removed. Run core and presentation tests, then commit.
4. **Comic Neue:** Bundle regular/bold TTF and license. Replace bitmap font model with measured font metrics and cached Skia fonts, keeping existing style roles/sizes and baseline alignment. Exercise text measurement, menu bounds and actual raster drawing; update content for desktop/browser and remove bitmap assets, then commit.
5. **Audio JSON:** Convert sound/music catalogs and SKM semantic data, including patterns, note/velocity mappings, envelopes, gains and samples. Preserve replay classes and numerical values, store sample Ogg separately. Replace binary reader and offline renderer/browser hash paths. Compare decoded metadata/replay output against the legacy source before removal; run tracker, desktop and packaging tests, then commit.

## Verification

Use serialized Release builds (`dotnet build Mooh.slnx -c Release --no-restore --disable-build-servers -m:1`) and executable xUnit assemblies; require a nonzero passing total. Run Python packaging tests and `git diff --check`. Verify final build from content directories without old binary/INI assets so stale copies cannot mask missing content. Do not claim interactive visual/audio acceptance from headless checks.


## Completed verification

- Compared all 62 converted atlases (6,623 effective frames) with the old reader's name, rectangle and signed-anchor output.
- Preserved 151 ordered waypoints and 457 localized string values, including embedded newlines.
- Compared the JSON tracker reader's complete semantic export with the old binary reader: all 49 patterns, 14 instruments and mappings matched, as did SHA-256 hashes of all 15 Ogg payloads.
- Full Release solution build passed, including native WebAssembly linking. Compiler/analyzer style and documentation warnings remain; no build errors.
- Fresh asset copies: 1,384 C# tests passed across all five suites. Python tooling: 21 passed. Browser JavaScript: 23 passed.
- Actual uncached offline rendering generated all three cues and the browser packager produced 747 files (19.8 MiB); recording hashes included the JSON and sample files.
- Inspected software-rendered main menu and options screenshots. Review caught bitmap-specific text overlay and checkbox-slot behavior; both were reproduced with failing tests, fixed, and included in the font checkpoint. GPU/browser interactive and audible playback acceptance were not performed.
