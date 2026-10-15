namespace Mooh.Presentation.Text
{
    /// <summary>One glyph of a laid-out line: its atlas rectangle and its screen position.</summary>
    /// <param name="SourceX">The glyph's left edge in the font atlas.</param>
    /// <param name="SourceY">The glyph's top edge in the font atlas.</param>
    /// <param name="Width">The glyph's width in pixels.</param>
    /// <param name="Height">The glyph's height in pixels.</param>
    /// <param name="X">The screen x of the glyph's left edge.</param>
    /// <param name="Y">The screen y of the glyph's top edge.</param>
    public readonly record struct GlyphPlacement(int SourceX, int SourceY, int Width, int Height, double X, double Y);
}
