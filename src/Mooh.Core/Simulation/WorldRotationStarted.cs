using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a rotator finished its <c>ST_Work</c> windup and the world began to spin. The
    /// simulation is frozen for the whole duration, because the recovered <c>Game.ModCycle</c>
    /// replaces the entire object cycle with <c>WorldRotCycle</c> while a rotation is in flight.
    /// </summary>
    /// <param name="EntityId">The rotator that started the turn.</param>
    /// <param name="Direction">The direction the world turns.</param>
    /// <param name="Duration">How long the spin lasts.</param>
    public sealed record WorldRotationStarted(
        EntityId EntityId,
        WorldRotationDirection Direction,
        TimeSpan Duration) : SimulationEvent(EntityId);
}
