namespace Mooh.Core.Levels
{
    /// <summary>
    /// Describes a recovered message table: a shadow object the player walks over, whose
    /// <c>PlHere</c> asks the help panel for its <c>_TableTxt</c>.
    /// </summary>
    /// <param name="helpTextId">
    /// The recovered help-text id, or <see langword="null"/> when the archival object carries none.
    /// </param>
    public sealed class MessageTableBody(string? helpTextId)
    {
        /// <summary>Gets the recovered help-text id, if the archival object carries one.</summary>
        public string? HelpTextId { get; } = helpTextId;

        /// <summary>
        /// Gets whether the table has been read. The recovered <c>_PlHere_Table</c> enters
        /// <c>ST_Work</c>, a single static frame with no end, so a read table stays drawn as read.
        /// </summary>
        public bool HasBeenRead
        {
            get; internal set;
        }
    }
}
