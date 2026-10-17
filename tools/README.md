# Tools

## Animation recovery

The original game builds its sprite state tables in Python 2.3 bytecode that the decompiler
could not fully recover: every list comprehension, which is where the frame names live, came
out empty. Two scripts rebuild those tables without executing the original code.

```bash
python3 -m venv .venv && .venv/bin/pip install xdis
.venv/bin/python -I tools/recover_animations.py <mooh_extracted> /tmp/animations-raw.json
python3 -I tools/normalize_animations.py /tmp/animations-raw.json assets/animations/animations.json
```

- `recover_animations.py` runs a small symbolic interpreter over every function of the game
  modules. Constants, `xrange`, string formatting and comprehension loops evaluate concretely;
  everything that needs the game's runtime stays a symbolic node.
- `normalize_animations.py` resolves state names, sprite pages, directions and constructor
  arguments into `assets/animations/animations.json`, which the presentation loads.

## Browser publishing

Install ffmpeg with the libmp3lame encoder, build the browser content bundle, then publish both runtime modes:

```bash
python3 tools/build_browser_content.py
python3 tools/publish_browser.py --no-aot --serve
```

The server uses `http://127.0.0.1:8080/` and sends the isolation headers required by
shared WebAssembly memory. Omit `--no-aot` for an AOT release publish, use `--serve 9000`
for a different port, or omit `--serve` to produce static files only. The output defaults
to `dist/browser/wwwroot`; `--output <directory>` changes the publish destination.

The publisher builds threaded and single-threaded runtimes concurrently in separate
artifact trees. It copies the fallback runtime unchanged into `_framework-single`,
merges the offline asset manifest, and updates the service worker version. A failed
build leaves the existing published site intact. Hosts without cross-origin isolation
use the fallback runtime automatically; `?mode=single` selects it explicitly.

Run the publisher and browser bootstrap regressions with:

```bash
python3 -m pytest -q tests/tools/test_publish_browser.py
node --test tests/browser/*.test.mjs
```

The content builder bundles only levels named by the authoritative manifest and preserves
original asset paths and pixels. It also renders `TrackMenu.skm` offline through `Mooh.MusicRenderer` and ffmpeg, exporting stereo MP3 recordings and exact loop boundaries from `Music.ini`. It keeps the original start and waits until consecutive loop cycles match before selecting the repeated audio section. Browser assets exclude the SKM module and its embedded samples. The recording regenerates when the module/catalog hash changes; delete `wwwroot/content/music` to force a new render. Its generated `wwwroot/content` folder is ignored; regenerate
it after changing game assets. Python tooling tests require `pytest`, and JavaScript tests
use Node's built-in test runner.

For real browser verification, start a separate Chromium test profile with a DevTools port,
serve the published site, then run:

```bash
node tools/browser_smoke.mjs 'http://127.0.0.1:8080/?mode=multi&screen=main' 9229 /tmp/mooh-smoke
```

The smoke test uses Node 22 or newer (built-in WebSocket), navigates through menus and gameplay,
checks pause/resume, update-dialog suspension, and resized rendering, and samples actual Web Audio PCM energy. Optional
`MOOH_STORAGE_TEST=1` and `MOOH_OFFLINE_TEST=1` verify saved options and a normal offline reload.
`MOOH_DISABLE_COI=1` disables isolation-worker registration for testing automatic fallback on a plain
HTTP server. Use an isolated browser profile so its save/cache checks stay separate from normal
browsing data.
