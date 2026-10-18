namespace Mooh.Presentation.Sprites
{
    /// <summary>One frame of a sprite page: its rectangle in the atlas image and its pivot.</summary>
    /// <param name="Name">The frame name, such as <c>OWlkLeft10</c>.</param>
    /// <param name="X">The left edge in the atlas image, in pixels.</param>
    /// <param name="Y">The top edge in the atlas image, in pixels.</param>
    /// <param name="Width">The width in pixels.</param>
    /// <param name="Height">The height in pixels.</param>
    /// <param name="PivotX">The horizontal offset from the frame's anchor to its left edge.</param>
    /// <param name="PivotY">The vertical offset from the frame's anchor to its top edge.</param>
    public sealed record SpriteFrame(string Name, int X, int Y, int Width, int Height, int PivotX, int PivotY);
}
