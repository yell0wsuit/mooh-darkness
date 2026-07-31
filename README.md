# Mooh Darkness

Mooh Darkness is a rewrite of the 2007 grid-based puzzle game, Crazy Chicken: Jewel of Darkness. The project targets .NET 10 and uses MonoGame for cross-platform compatibility.

The current milestone is deliberately headless. `Mooh.Core` implements authoritative grid occupancy, interaction masks, movement, atomic pushing, and continuous directional gravity without graphics dependencies. Gravity-driven bodies retain fractional progress while crossing cells, round bodies can slide around support, and each update returns an ordered trace of fall, slide, cell-entry, and landing events.

Rendering and complete collision physics are outside the current milestone.

## Build and test

```bash
dotnet build Mooh.slnx
dotnet test Mooh.slnx
```

The recovered Python 2.3 source is research material only; the new implementation does not execute or depend on it.
