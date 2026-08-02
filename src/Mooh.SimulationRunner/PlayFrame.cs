using System.Collections.Generic;

namespace Mooh.SimulationRunner
{
    /// <summary>Describes one rendered frame of an interactive play session.</summary>
    /// <param name="Rows">One emoji string per grid row.</param>
    /// <param name="Level">The one-based level number being played.</param>
    /// <param name="LevelCount">The number of levels in the pack.</param>
    /// <param name="Score">The monotonic cumulative score.</param>
    /// <param name="Health">The player's current health.</param>
    /// <param name="Grenades">The player's remaining grenades.</param>
    /// <param name="MonstersLeft">The number of level-end lock carriers still alive.</param>
    /// <param name="State">The session state name.</param>
    /// <param name="Reaction">The player's damage reaction name.</param>
    public sealed record PlayFrame(
        IReadOnlyList<string> Rows,
        int Level,
        int LevelCount,
        int Score,
        double Health,
        int Grenades,
        int MonstersLeft,
        string State,
        string Reaction);
}
