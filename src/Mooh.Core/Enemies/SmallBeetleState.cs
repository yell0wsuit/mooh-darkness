namespace Mooh.Core.Enemies
{
    /// <summary>Identifies a spawned small beetle's flight state.</summary>
    public enum SmallBeetleState
    {
        /// <summary>The beetle is on a cell and picks its next flight direction.</summary>
        Deciding,

        /// <summary>The beetle is in transit toward the cell it has already committed to.</summary>
        Flying,

        /// <summary>
        /// Every candidate direction was blocked, so the beetle waits out the recovered
        /// <c>CNT_OnCellReCheck</c> delay before deciding again.
        /// </summary>
        Waiting,
    }
}
