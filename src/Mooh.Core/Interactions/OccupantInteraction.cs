namespace Mooh.Core.Interactions
{
    /// <summary>
    /// Describes how a mover interacts with an occupant of its destination cell.
    /// </summary>
    public enum OccupantInteraction
    {
        /// <summary>The mover may share the occupant's cell.</summary>
        Allow,

        /// <summary>The occupant prevents the move.</summary>
        Block,

        /// <summary>The occupant must move in the same direction before the mover.</summary>
        Push,
    }
}
