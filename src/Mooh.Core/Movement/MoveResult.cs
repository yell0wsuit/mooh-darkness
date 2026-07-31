namespace Mooh.Core.Movement
{
    /// <summary>
    /// Describes the outcome of a movement request.
    /// </summary>
    public enum MoveResult
    {
        /// <summary>The requested entity moved without pushing another entity.</summary>
        Moved,

        /// <summary>The requested entity moved and pushed at least one entity.</summary>
        Pushed,

        /// <summary>The world was unchanged because movement was blocked.</summary>
        Blocked,
    }
}
