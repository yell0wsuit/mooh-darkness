# Mooh Darkness

Mooh Darkness is a rewrite of the 2007 grid-based puzzle game, Crazy Chicken: Jewel of Darkness. The project targets .NET 10.

The current milestone is deliberately headless. `Mooh.Core` loads and validates the committed 150-level authoritative JSON pack into strongly typed archival models without creating runtime simulation entities. It also enforces a zero-or-one-player simulation world, interprets held cardinal input, and advances the player continuously between authoritative grid cells. Walking supports immediate same-axis reversal, turns only at cell boundaries, and preserves occupancy until a validated crossing. Push chains move continuously at their configured multiplier, continue independently until blocked, and hand off to falling or round-object sliding when gravity takes over. Each update returns an ordered trace of player, push, gravity, and cell-entry events.

Runtime materialization of archival level objects, MonoGame integration, rendering, keyboard and gamepad input adapters, and complete collision physics are outside the current milestone.

## Build and test

```bash
dotnet build Mooh.slnx
dotnet test Mooh.slnx
```

## Deterministic simulation scenarios

`Mooh.SimulationRunner` executes self-verifying headless simulation scenarios. Each JSON
file declares a runtime grid, ordered entities, a random seed, timed held inputs, the exact
event trace for every update, and the expected final entity state.

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

The command prints one emoji per grid cell, favors gameplay objects over underlying terrain
when a cell has multiple occupants, includes a legend, and reports any unclassified legacy
object types. This is an observational content tool; it does not create runtime simulation
entities or assign gameplay behavior to legacy object types.

The recovered Python 2.3 source is research material only; the new implementation does not execute or depend on it.
