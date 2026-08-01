# Mooh Darkness

Mooh Darkness is a rewrite of the 2007 grid-based puzzle game, Crazy Chicken: Jewel of Darkness. The project targets .NET 10.

The current milestone is deliberately headless. `Mooh.Core` loads and validates the committed 150-level authoritative JSON pack into strongly typed archival models without creating runtime simulation entities. It also enforces a zero-or-one-player simulation world, interprets held cardinal input, and advances the player continuously between authoritative grid cells. Walking supports immediate same-axis reversal, turns only at cell boundaries, and preserves occupancy until a validated crossing. Push chains move continuously at their configured multiplier, continue independently until blocked, and hand off to falling or round-object sliding when gravity takes over. Each update returns an ordered trace of player, push, gravity, and cell-entry events.

Runtime materialization of archival level objects, MonoGame integration, rendering, keyboard and gamepad input adapters, and complete collision physics are outside the current milestone.

## Build and test

```bash
dotnet build Mooh.slnx
dotnet test Mooh.slnx
```

The recovered Python 2.3 source is research material only; the new implementation does not execute or depend on it.
