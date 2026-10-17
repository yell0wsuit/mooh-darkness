namespace Mooh.Presentation.Scene
{
    /// <summary>A floating line of text in the world, drawn over it.</summary>
    /// <param name="Font">The Comic Neue font.</param>
    /// <param name="Text">The text.</param>
    /// <param name="X">The column of the line's center.</param>
    /// <param name="Y">The row of the line's baseline.</param>
    /// <param name="Opacity">How opaque the line is, from 0 to 1.</param>
    public readonly record struct SceneText(string Font, string Text, double X, double Y, double Opacity);
}
