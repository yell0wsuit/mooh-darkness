using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>Identifies the result of an object entering a fire cell.</summary>
    public enum FireContactKind
    {
        /// <summary>The player spent the fire and received a burn request.</summary>
        Player,
        /// <summary>The object and fire destroyed each other.</summary>
        MutualDestruction,
        /// <summary>The fire consumed the object and survived.</summary>
        Consumed,
    }

    /// <summary>Reports an arriving object resolving its contact with fire.</summary>
    /// <param name="EntityId">The fire.</param>
    /// <param name="Position">The contact cell.</param>
    /// <param name="Kind">The interaction result.</param>
    public sealed record FireContactResolved(EntityId EntityId, GridPosition Position, FireContactKind Kind) : SimulationEvent(EntityId);
}
