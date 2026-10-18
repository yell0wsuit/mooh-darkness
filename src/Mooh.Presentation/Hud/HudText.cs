using Mooh.Presentation.Text;

namespace Mooh.Presentation.Hud
{
    /// <summary>One line of text to draw.</summary>
    /// <param name="Text">The text.</param>
    /// <param name="X">The screen x of the line's point.</param>
    /// <param name="Y">The screen y of the line's point.</param>
    /// <param name="Alignment">Where the line sits against its point.</param>
    /// <param name="Font">The font, <see cref="HudPresenter.FontName"/> unless stated.</param>
    public readonly record struct HudText(string Text, double X, double Y, TextAlignment Alignment, string Font = HudPresenter.FontName);
}
