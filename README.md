# Mooh Darkness

Mooh Darkness is a rewrite of the 2007 grid-based puzzle game, Crazy Chicken: Jewel of Darkness. The project targets .NET 10.

The desktop port uses SDL3 and SkiaSharp and includes the shipped 150-level pack, gameplay, original sprites and bitmap fonts, sound effects and module music. The original screen flow is connected: intro, main menu, instructions/loading, map, level introduction, gameplay, pause/options/restart, level statistics, high scores, campaign completion and outro. Dormant object types and unreachable editor/registration menus are excluded.

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

`--screen` also accepts `intro`, `main`, `instructions`, `options`, `scores`, `prelevel`, `pause`, `statistics`, `complete` and `outro`. Screen inspection uses a temporary in-memory profile. Rendering preserves the original 800×600 layout with letterboxing; the renderer can be selected with `--renderer metal|opengl|software`.

## Build and test

```bash
dotnet build Mooh.slnx
dotnet tests/Mooh.Core.Tests/bin/Debug/net10.0/Mooh.Core.Tests.dll
dotnet tests/Mooh.Presentation.Tests/bin/Debug/net10.0/Mooh.Presentation.Tests.dll
dotnet tests/Mooh.Desktop.Tests/bin/Debug/net10.0/Mooh.Desktop.Tests.dll
dotnet tests/Mooh.Desktop.Platform.Tests/bin/Debug/net10.0/Mooh.Desktop.Platform.Tests.dll
dotnet tests/Mooh.SimulationRunner.Tests/bin/Debug/net10.0/Mooh.SimulationRunner.Tests.dll
```

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
