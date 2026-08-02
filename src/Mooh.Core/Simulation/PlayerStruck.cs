using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a hand strike landed on the faced cell. The recovered <c>_AEnd_Str</c> then
    /// consumes what the cell holds and pushes what carries <c>CNT_OrMaskPlPush</c>.
    /// </summary>
    /// <param name="EntityId">The striking player.</param>
    /// <param name="Position">The struck cell.</param>
    /// <param name="Direction">The struck direction.</param>
    public sealed record PlayerStruck(
        EntityId EntityId,
        GridPosition Position,
        Direction Direction) : SimulationEvent(EntityId);
}
