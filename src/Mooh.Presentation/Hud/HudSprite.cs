namespace Mooh.Presentation.Hud
{
    /// <summary>One panel frame to draw from <see cref="HudPresenter.PageName"/>.</summary>
    /// <param name="Frame">The frame name, such as <c>PNrmHealth</c>.</param>
    /// <param name="X">The screen x of the frame's anchor.</param>
    /// <param name="Y">The screen y of the frame's anchor.</param>
    public readonly record struct HudSprite(string Frame, int X, int Y);
}
