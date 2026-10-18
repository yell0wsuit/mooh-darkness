namespace Mooh.Core.Grid
{
    /// <summary>
    /// Stores transient simulation state associated with one world cell.
    /// </summary>
    internal sealed class CellState
    {
        /// <summary>Gets or sets the simulation time at which gravity may use the cell again.</summary>
        public double LockedUntilSeconds
        {
            get; set;
        }
    }
}
