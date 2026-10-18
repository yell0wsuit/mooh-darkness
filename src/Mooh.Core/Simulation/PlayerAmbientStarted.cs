using System;

using Mooh.Core.Entities;
using Mooh.Core.Player;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that an idle player started an ambient, the recovered <c>AmbDo</c>, and now faces
    /// down.
    /// </summary>
    /// <param name="EntityId">The player.</param>
    /// <param name="Kind">The ambient <c>App.Rand.List(ST_AmbAll)</c> picked.</param>
    /// <param name="Duration">How long the ambient plays if nothing interrupts it.</param>
    public sealed record PlayerAmbientStarted(EntityId EntityId, PlayerAmbientKind Kind, TimeSpan Duration)
        : SimulationEvent(EntityId);
}
