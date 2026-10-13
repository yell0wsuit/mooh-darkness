using System.Collections.Generic;

namespace Mooh.SimulationRunner
{
    /// <summary>Describes one rendered frame of an interactive play session.</summary>
    /// <param name="Sprites">Every entity at its interpolated position, in ascending draw order.</param>
    /// <param name="Width">The world width in cells.</param>
    /// <param name="Height">The world height in cells.</param>
    /// <param name="Level">The one-based level number being played.</param>
    /// <param name="LevelCount">The number of levels in the pack.</param>
    /// <param name="Score">The monotonic cumulative score.</param>
    /// <param name="Health">The player's current health.</param>
    /// <param name="Grenades">The player's remaining grenades.</param>
    /// <param name="MonstersLeft">The number of level-end lock carriers still alive.</param>
    /// <param name="State">The session state name.</param>
    /// <param name="Reaction">The player's damage reaction name.</param>
    /// <param name="HelpText">
    /// The recovered help-text id of the message table read most recently, while the help panel
    /// would still be showing it, or <see langword="null"/>.
    /// </param>
    /// <param name="Weapon">The current weapon's name.</param>
    /// <param name="Rockets">The player's remaining rockets.</param>
    /// <param name="Mines">The player's remaining mines.</param>
    /// <param name="BigBombs">The player's remaining big bombs.</param>
    /// <param name="TimeRemaining">
    /// The timed-play clock in seconds, which replaces health on the timed difficulty, or
    /// <see langword="null"/> when health is the live axis.
    /// </param>
    public sealed record PlayFrame(
        IReadOnlyList<PlaySprite> Sprites,
        int Width,
        int Height,
        int Level,
        int LevelCount,
        int Score,
        double Health,
        int Grenades,
        int MonstersLeft,
        string State,
        string Reaction,
        string? HelpText = null,
        double? TimeRemaining = null,
        string Weapon = "Grenade",
        int Rockets = 0,
        int Mines = 0,
        int BigBombs = 0);
}
