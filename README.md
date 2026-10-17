# Mooh Darkness

Mooh Darkness is a rewrite of the 2007 grid-based puzzle game, Crazy Chicken: Jewel of Darkness. The project targets .NET 10.

The desktop port uses SDL3 and SkiaSharp and includes the shipped 150-level pack, gameplay, original sprites and bitmap fonts, sound effects and module music. The original screen flow is connected: intro, main menu, map, level introduction, gameplay, pause/options/restart, level statistics, high scores, campaign completion and outro. Dormant object types and unreachable editor/registration menus are excluded.

Run the game with:

```bash
dotnet run --project src/Mooh.Desktop
```

Use the arrow keys to move, Space to fire, Enter to cycle weapons, and 1–4 to select a weapon. Escape, P or Pause opens the pause menu; F2 opens restart confirmation and F10 opens options from a menu. Menus support keyboard selection and mouse input. The map makes passed stages and the next unpassed stage available.

Progress, options and high scores use JSON in `MoohDarkness_SaveData/player-profile.json`. The save folder follows the other DX ports: beside the executable when writable (except inside a macOS app bundle), then Documents, then local application data. Continue restarts the saved level with the cumulative score, matching the original save contract. Timed runs do not retain a Continue slot.

For a direct gameplay or screen capture:

```bash
dotnet run --project src/Mooh.Desktop -- --level 1 --seconds 10 --screenshot gameplay.png
dotnet run --project src/Mooh.Desktop -- --screen map --renderer software --seconds 1 --screenshot map.png
```

`--screen` also accepts `intro`, `main`, `options`, `scores`, `prelevel`, `pause`, `statistics`, `complete` and `outro`. Screen inspection uses a temporary in-memory profile. New Game and Continue proceed directly when their data is ready, without a decorative loading timer or keypress gate. The default window is 1280×720. Gameplay expands horizontally on wide windows and vertically on tall windows at the original sprite scale. HUD panels stay within a centered 16:9 area on wide windows and the original 800×600 area on tall windows; fixed 800×600 screens stay centered over a Gaussian-blurred backdrop. Resizing to 4:3 preserves the original gameplay view; the renderer can be selected with `--renderer metal|vulkan|opengl|software`.

Desktop rendering automatically tries Vulkan before OpenGL/software on Windows and Linux. Vulkan requires a Vulkan 1.1 loader and driver; `--renderer vulkan` forces it. macOS retains Metal/OpenGL/software because its Skia native package does not provide Vulkan.

Visible space beyond the level boundary is filled with generated terrain matching the level, small patches of solid wood, stone and plant-covered blocks, connected pipe runs, and scattered decorative rocks. Rocks rest directly on dirt or ordinary solid terrain; on rounded supports such as metal and pipes, both roll-off routes must be blocked by side walls or a shelf below. Unsafe placements use matching dirt instead. The scenery stays fixed while moving or resizing the window and follows world rotations; placement rules are rechecked after each turn. Scenery exists only in memory and has no collisions; level JSON and its existing `size` remain unchanged.

The legacy “Highest tone” color-depth option is removed; current hosts always render in full color.

## Browser version

Install ffmpeg and the .NET 10 `wasm-tools` workload, then package the original game assets and publish:

```bash
dotnet workload install wasm-tools
python3 tools/build_browser_content.py
python3 tools/publish_browser.py --serve
```

The AOT release runs at `http://127.0.0.1:8080/`. Use `--no-aot` for a quicker interpreter build. Deploy the contents of `dist/browser/wwwroot` to a static HTTPS host. The publisher builds a worker-owned WebGL2 runtime and a single-threaded fallback; the page selects the supported mode before transferring its canvas. COOP/COEP headers enable threading, with a cross-origin isolation service worker for hosts such as GitHub Pages. Use `?mode=single` to select the fallback explicitly.

The browser shares desktop gameplay, menus, rendering, and bitmap fonts. Its music is rendered offline to 192 kbps stereo Opus in Ogg during content packaging, then played as a continuous browser audio buffer with the original menu/game/ending start positions and gains; browser sound effects are converted to 96 kbps mono Opus in Ogg. Desktop retains its original Skale playback and Vorbis effects. Progress, options and high scores persist in localStorage. The PWA caches the selected runtime and game assets for subsequent offline launches. Keyboard controls match desktop; touch can operate menus, with mobile gameplay controls reserved for later. `?screen=main` or `?level=1` supports direct inspection after pressing Play.

## Build and test

```bash
dotnet build Mooh.slnx
# Desktop-only build (no browser workload needed):
dotnet build src/Mooh.Desktop
dotnet tests/Mooh.Core.Tests/bin/Debug/net10.0/Mooh.Core.Tests.dll
dotnet tests/Mooh.Presentation.Tests/bin/Debug/net10.0/Mooh.Presentation.Tests.dll
dotnet tests/Mooh.Desktop.Tests/bin/Debug/net10.0/Mooh.Desktop.Tests.dll
dotnet tests/Mooh.Desktop.Platform.Tests/bin/Debug/net10.0/Mooh.Desktop.Platform.Tests.dll
dotnet tests/Mooh.SimulationRunner.Tests/bin/Debug/net10.0/Mooh.SimulationRunner.Tests.dll
```

## GitHub Actions and releases

The workflows follow Contre Jour DX: PRs and pushes to `main` check formatting, build the solution, and run the five managed test suites plus desktop packaging tests. Browser/web test suites remain local and are not run in CI.

Run **Deploy Browser to GitHub Pages** manually after selecting GitHub Actions as the repository's Pages source. It installs ffmpeg as a build dependency, caches converted Opus content and SFX, publishes both AOT browser runtimes, and deploys `wwwroot`. No ffmpeg binary ships with the site or desktop game.

Pushing a `vX.Y.Z` tag or manually running **Release** creates Windows x64 `.7z`, macOS arm64 `.dmg`, and Linux x64 `.AppImage` bundles. A `feat:` or `fix:` commit pushed to `main` creates a `0.1.0-prerelease+<run number>` through the same release workflow. Linux packages retain a bundled .NET runtime rather than NativeAOT for compatibility. Cache Cleanup removes closed-PR caches daily and when same-repository PRs close.

Local packaging commands:

```bash
python3 distribution/release_windows.py 0.1.0
USE_AOT=true ./distribution/bundle_macos.sh 0.1.0
./distribution/build_appimage.sh 0.1.0
```

Artifacts are written to `src/Mooh.Desktop/bin/release_github`. Windows packaging requires 7-Zip; Linux downloads appimagetool. Desktop packages use the original raw assets, and macOS keeps them under the app's Resources directory with an executable-relative symlink.

## Deterministic simulation scenarios

`Mooh.SimulationRunner` executes self-verifying headless simulation scenarios. Each JSON
file declares a runtime grid, ordered entities, a random seed, timed held inputs, the exact
event trace for every update, and the expected final entity state. Supported runtime
components include gravity, player control, health, and green- or blue-monster behavior; scenarios
can assert final health, immunity, player reaction, and monster phase/timers.

Run one scenario or every top-level JSON scenario in a directory:

```bash
dotnet run --project src/Mooh.SimulationRunner -- scenarios/001-falling-and-landing.json
dotnet run --project src/Mooh.SimulationRunner -- scenarios
```

The runner prints one `PASS` or `FAIL` line per scenario and returns a nonzero exit code for
invalid source data or an expectation mismatch. These scenarios construct supported runtime
components directly; they do not materialize objects from the archival level corpus.

## Emoji level board

Inspect the logical board of one authoritative archival level without starting a renderer:

```bash
dotnet run --project src/Mooh.SimulationRunner -- level-board assets/levels/tutor_0009_mon_green_attack.json
```

The command prints one emoji per grid cell—including `🟢` and `🔵` for the two monsters—favors gameplay objects over underlying terrain
when a cell has multiple occupants, includes a legend, and reports any unclassified legacy
object types. This is an observational content tool; it does not create runtime simulation
entities or assign gameplay behavior to legacy object types.

The recovered Python 2.3 source is research material only; the new implementation does not execute or depend on it.
