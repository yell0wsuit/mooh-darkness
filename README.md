# Mooh Darkness

Mooh Darkness is a rewrite of the 2007 grid-based puzzle game, Crazy Chicken: Jewel of Darkness. The project targets .NET 10 and use MonoGame for cross-platform compatibility.

The current milestone is deliberately headless. `Mooh.Core` implements authoritative grid occupancy, interaction masks, movement, and atomic pushing without graphics dependencies.

## Build and test

```bash
dotnet build Mooh.slnx
dotnet test Mooh.slnx
```

The recovered Python 2.3 source is research material only; the new implementation does not execute or depend on it.
