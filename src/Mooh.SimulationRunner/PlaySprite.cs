namespace Mooh.SimulationRunner
{
    /// <summary>Describes one drawable entity at its interpolated sub-cell position.</summary>
    /// <param name="Glyph">The emoji drawn for the entity.</param>
    /// <param name="X">The horizontal position in cells, including the trailing transit fraction.</param>
    /// <param name="Y">The vertical position in cells, including the trailing transit fraction.</param>
    /// <param name="Layer">The draw order; higher layers are drawn last.</param>
    public sealed record PlaySprite(string Glyph, double X, double Y, int Layer);
}
