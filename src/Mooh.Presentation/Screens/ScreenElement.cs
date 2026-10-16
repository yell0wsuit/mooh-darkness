using System.Collections.Generic;

using Mooh.Presentation.Text;

namespace Mooh.Presentation.Screens
{
    /// <summary>A draw command in the original 800 by 600 screen coordinate space.</summary>
    public abstract record ScreenElement;

    /// <summary>A group drawn within a screen-space clipping rectangle.</summary>
    /// <param name="X">The clip's left edge.</param>
    /// <param name="Y">The clip's top edge.</param>
    /// <param name="Width">The clip's width.</param>
    /// <param name="Height">The clip's height.</param>
    /// <param name="Elements">The ordered draw commands inside the clip.</param>
    public sealed record ScreenClip(double X, double Y, double Width, double Height, IReadOnlyList<ScreenElement> Elements) : ScreenElement;

    /// <summary>A full image, identified by its file name within the screen assets.</summary>
    /// <param name="Name">The image file name, including its extension.</param>
    /// <param name="X">The left edge.</param>
    /// <param name="Y">The top edge.</param>
    /// <param name="Width">The displayed width.</param>
    /// <param name="Height">The displayed height.</param>
    /// <param name="Opacity">The opacity between zero and one.</param>
    public sealed record ScreenImage(string Name, double X, double Y, double Width, double Height, double Opacity = 1) : ScreenElement;

    /// <summary>An atlas frame anchored at a screen point.</summary>
    /// <param name="Page">The sprite page name.</param>
    /// <param name="Frame">The frame name.</param>
    /// <param name="X">The anchor's horizontal position.</param>
    /// <param name="Y">The anchor's vertical position.</param>
    /// <param name="Opacity">The opacity between zero and one.</param>
    public sealed record ScreenSprite(string Page, string Frame, double X = 0, double Y = 0, double Opacity = 1) : ScreenElement;

    /// <summary>A repeating atlas frame covering the logical screen.</summary>
    /// <param name="Page">The sprite page name.</param>
    /// <param name="Frame">The tile frame name.</param>
    public sealed record ScreenTile(string Page, string Frame) : ScreenElement;

    /// <summary>A line of bitmap text.</summary>
    /// <param name="Text">The text to draw.</param>
    /// <param name="X">The horizontal anchor.</param>
    /// <param name="Y">The text baseline.</param>
    /// <param name="Font">The bitmap font name.</param>
    /// <param name="Alignment">The horizontal alignment.</param>
    /// <param name="Opacity">The opacity between zero and one.</param>
    public sealed record ScreenText(string Text, double X, double Y, string Font = "MenuFntBtnNrm", TextAlignment Alignment = TextAlignment.Center, double Opacity = 1) : ScreenElement;
}

