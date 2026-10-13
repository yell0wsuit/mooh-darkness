using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player walked onto a message table, the recovered
    /// <c>_PlHere_Table</c> calling <c>GMod.HlpAdd</c>. The text itself lives in a localized string
    /// table that was not recovered, so the event carries the recovered id; the panel's slide and
    /// hold timings are presentation.
    /// </summary>
    /// <param name="EntityId">The table that was read.</param>
    /// <param name="HelpTextId">The recovered <c>_TableTxt</c> id, such as <c>HlpTutAllObjsFire</c>.</param>
    public sealed record HelpTextRequested(EntityId EntityId, string HelpTextId) : SimulationEvent(EntityId);
}
