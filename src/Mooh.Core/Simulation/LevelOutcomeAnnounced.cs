using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports the moment <c>__GStatePlay</c> announces a decided level with <c>TxtAdd</c>: a win
    /// after the recovered 500 ms pause, a loss at once, and a timeout after its 500 ms pause. The
    /// level settles later, with <see cref="LevelWon"/> or <see cref="LevelLost"/>.
    /// </summary>
    /// <param name="EntityId">The player entity the outcome belongs to.</param>
    /// <param name="Outcome">The outcome being announced.</param>
    /// <param name="TimedOut">Whether the loss is the timed level's clock running out.</param>
    public sealed record LevelOutcomeAnnounced(EntityId EntityId, LevelOutcome Outcome, bool TimedOut) : SimulationEvent(EntityId);
}
