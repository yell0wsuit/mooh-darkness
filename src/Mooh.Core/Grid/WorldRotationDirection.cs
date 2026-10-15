namespace Mooh.Core.Grid
{
    /// <summary>
    /// The direction a world rotation turns the level through, named after the recovered
    /// <c>GMod.WorldRotStart(Owner, bLeft)</c> argument the two placed object types supply.
    /// </summary>
    public enum WorldRotationDirection
    {
        /// <summary>
        /// The turn <c>OWRotL</c> requests. <c>GameObjs.WorldRotLeft</c> passes <c>bLeft=True</c>,
        /// which selects the positive <c>CNT_GPrm_WorldRotSp</c> and maps coordinates to
        /// <c>(height - 1 - y, x)</c>.
        /// </summary>
        Left,

        /// <summary>
        /// The turn <c>OWRotR</c> requests. <c>GameObjs.WorldRotRight</c> passes
        /// <c>bLeft=False</c>, which selects the negative speed and maps coordinates to
        /// <c>(y, width - 1 - x)</c>.
        /// </summary>
        Right,
    }
}
