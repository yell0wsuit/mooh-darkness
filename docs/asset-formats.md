# Runtime asset formats

Runtime readers use `System.Text.Json` without reflection so they also work in trimmed desktop
and browser builds. Legacy SEQ, waypoint, STR2, bitmap-font, INI and SKM files are not shipped.
The 2026-10-17 conversion preserved the original effective frame tables, all 151 waypoints,
457 strings, audio aliases and tracker state. Duplicate legacy sprite names retained their
last definition, matching the old loader.

## Sprites

`assets/sprites/Sprite*.json` uses TexturePacker JSON Hash or Array, adapted from Contre Jour DX.
Names retain the game's existing frame IDs; an optional `.png` suffix is removed. Each entry
contains `frame` (`x`, `y`, `w`, `h`), `rotated: false`, `spriteSourceSize`, `sourceSize` and
`pivot`. The normalized pivot can lie outside the source rectangle. The visible anchor offset
is `spriteSourceSize.x - pivot.x * sourceSize.w` (and likewise for y), rounded to the integer
coordinates used by Mooh. Empty frames draw nothing and retain a 1-pixel logical source size
to encode their anchors without division by zero. Atlas pixels and animation frame names are
unchanged. Pack without rotation or resizing (`meta.scale: "1"`). Images keep the page basename:
`sprites/<page>.png`, or `screens/<page>.jpg` for recovered screen atlases. `meta.image` is
descriptive; the current renderer resolves the image from that basename.

## Map and strings

`assets/map/map.json` is an ordered array of `{ "x": 407, "y": 434 }` integer coordinates.
Order is significant: the first 150 are levels and paths; the final point is unused.

`assets/text/strings.json` contains `language` and a `strings` object keyed by text ID. Values
are Unicode strings; JSON `\n` escapes become line breaks. Existing `%d` placeholders remain
unchanged.

## Fonts

`assets/fonts/ComicNeue-Regular.ttf` and `ComicNeue-Bold.ttf` replace all bitmap atlases.
`GameFont` maps existing style roles to a face, cap height, baseline offset and color. Layout
and drawing use the same Skia font metrics; the renderer caches faces until disposal, following
Contre Jour DX's Skia font approach. `FontAppearance` preserves each original bitmap role's
sampled fill colors and edge treatment: black outlines where present, plain black story/help
text, gold title gradients with a gray bevel and black shadow, white tooltips with orange-red
edges, and green/purple edging on floating scores. Normal, selected and pressed buttons keep
separate gold, yellow and dark-gold fills. Edge widths and offsets use logical screen pixels.
Menus, map controls and HUD values use Comic Neue Bold. Stories, tooltips, explanatory text
and version fine print use Comic Neue Regular. Button outlines use a centered two-pixel black
rim, and large HUD labels use a two-pixel rim, keeping counters and letter spacing open.
Glyph fills are drawn without synthetic thickening; the original colors remain unchanged.
Fades apply to the complete composed glyph so overlapping outlines do not accumulate opacity.
The original text animation timing is retained.

## Sound, music cues and tracker modules

`assets/sounds/Sound.json` maps aliases to `files` (without `.ogg`), `buffers`, `volume` and
`frequency`. Each range is `[minimum, maximum]`; an empty file array is deliberately silent.
Disabled legacy aliases remain absent.

`assets/music/Music.json` maps cue names to `file` (module basename), `position` (order index)
and `volume` (master cue gain).

`assets/music/TrackMenu.json` is version 1 tracker data. It contains title, tempo, speed, mixer
gain, order list, song length, channel count, patterns and instruments. Pattern channels contain
ordered note/instrument/volume/effect/parameter cells. Instruments retain all 128 note mappings,
velocity groups, fixed-note flags, effect mappings, envelope selections and envelope points.
Samples retain tuning, length and loop metadata and reference the original Ogg payloads under
`samples/`; `file: null` represents the two samples with no payload. Loading a module path
resolves samples relative to that directory. No binary tracker parser is used at runtime.

Desktop loads the module and samples directly. Browser packaging renders the same module
through `Mooh.MusicRenderer` and ffmpeg to Opus, keeping original cue positions and exact loop
boundaries. The source hash includes the module JSON, cue JSON and all sample files in filename
order, so changing a sample invalidates cached recordings.
