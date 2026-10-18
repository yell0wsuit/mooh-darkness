using Mooh.Core.Entities;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Reports that the player walked onto a message table, the recovered
    /// <c>_PlHere_Table</c> calling <c>GMod.HlpAdd</c>. The event carries the recovered id, which
    /// the localized <see cref="Content.StringTable"/> resolves; the panel's slide and hold timings
    /// are presentation.
    /// </summary>
    /// <param name="EntityId">The table that was read.</param>
    /// <param name="HelpTextId">The recovered <c>_TableTxt</c> id, such as <c>HlpTutAllObjsFire</c>.</param>
    public sealed record HelpTextRequested(EntityId EntityId, string HelpTextId) : SimulationEvent(EntityId);
}
