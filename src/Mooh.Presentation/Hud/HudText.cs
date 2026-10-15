using Mooh.Presentation.Text;

namespace Mooh.Presentation.Hud
{
    /// <summary>One line of text to draw in <see cref="HudPresenter.FontName"/>.</summary>
    /// <param name="Text">The text.</param>
    /// <param name="X">The screen x of the line's point.</param>
    /// <param name="Y">The screen y of the line's point.</param>
    /// <param name="Alignment">Where the line sits against its point.</param>
    public readonly record struct HudText(string Text, double X, double Y, TextAlignment Alignment);
}
