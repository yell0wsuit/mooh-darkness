namespace Mooh.Presentation.Text
{
    /// <summary>Where a line of text sits against its point, the native <c>TextOut*</c> family.</summary>
    public enum TextAlignment
    {
        /// <summary>The text starts at the point, <c>TextOutLeft</c>.</summary>
        Left,

        /// <summary>
        /// The text starts half its width, rounded down, left of the point, <c>TextOutCenter</c>.
        /// </summary>
        Center,

        /// <summary>The text ends at the point, <c>TextOutRight</c>.</summary>
        Right,
    }
}
