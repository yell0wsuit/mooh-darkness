using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that a struck object's push chain stopped against the cell ahead, the recovered
    /// <c>PlPush_OnCell</c> blocked branch, after the shakes it applied there. The original jerks
    /// the view along the push and plays the object's landing sound at that moment.
    /// </summary>
    /// <param name="EntityId">The stopped object.</param>
    /// <param name="Direction">The direction it was pushed in.</param>
    public sealed record StrikePushStopped(EntityId EntityId, Direction Direction) : SimulationEvent(EntityId);
}
