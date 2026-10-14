using System.Collections.Generic;

namespace Mooh.Presentation.Animation
{
    /// <summary>
    /// An extra animation an object draws with its own frame, such as a weapon's glow,
    /// <c>GlowAnm</c>, or food's sparkle, <c>OverAnm</c>; both are drawn additively.
    /// </summary>
    /// <param name="Page">The sprite page.</param>
    /// <param name="Frames">The frames.</param>
    /// <param name="FramesPerMillisecond">The recovered speed, frames per millisecond.</param>
    public sealed record LayerAnimation(string Page, IReadOnlyList<string> Frames, double FramesPerMillisecond);
}
