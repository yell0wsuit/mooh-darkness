namespace Mooh.Presentation.Sprites
{
    /// <summary>
    /// Tells which atlas pixels are solid, the 1-bit mask a page loaded with
    /// <c>PageMake(name, True)</c> keeps for <c>TransPntCheck</c>.
    /// </summary>
    public interface ISpriteMask
    {
        /// <summary>Gets a frame's rectangle and anchor.</summary>
        /// <param name="page">The sprite page.</param>
        /// <param name="frame">The frame name.</param>
        /// <returns>The frame, or <see langword="null"/> when the page has no such frame.</returns>
        SpriteFrame? FrameOf(string page, string frame);

        /// <summary>Tells whether an atlas pixel is solid.</summary>
        /// <param name="page">The sprite page.</param>
        /// <param name="x">The pixel's column in the atlas.</param>
        /// <param name="y">The pixel's row in the atlas.</param>
        /// <returns><see langword="true"/> when the pixel is solid.</returns>
        bool IsSolid(string page, int x, int y);
    }
}
