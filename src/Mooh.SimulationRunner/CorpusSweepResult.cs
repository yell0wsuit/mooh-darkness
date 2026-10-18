using Mooh.Core.Simulation;

namespace Mooh.SimulationRunner
{
    /// <summary>Reports how one level fared under the sweep bot.</summary>
    /// <param name="Name">The level's manifest name.</param>
    /// <param name="Outcome">The level outcome when the sweep stopped.</param>
    /// <param name="Score">The player's level score when the sweep stopped.</param>
    /// <param name="EventCount">The number of simulation events the level produced.</param>
    /// <param name="Failure">The exception that stopped the level, or <see langword="null"/>.</param>
    public sealed record CorpusSweepResult(
        string Name,
        LevelOutcome Outcome,
        int Score,
        int EventCount,
        string? Failure);
}
