using System.Collections.Generic;

using SkiaSharp;

namespace Mooh.Presentation.Text
{
    /// <summary>One outline, bevel or shadow drawn behind a TrueType glyph.</summary>
    /// <param name="Color">The source atlas edge color.</param>
    /// <param name="Width">The full Skia stroke width in logical pixels.</param>
    /// <param name="X">The horizontal offset.</param>
    /// <param name="Y">The vertical offset.</param>
    /// <param name="Blur">The optional shadow blur sigma.</param>
    public readonly record struct FontEdge(SKColor Color, float Width, float X = 0, float Y = 0, float Blur = 0);

    /// <summary>Original bitmap font colors and edge treatments applied to Comic Neue glyphs.</summary>
    /// <param name="Color">The solid fill or top of the vertical gradient.</param>
    /// <param name="BottomColor">The bottom of the vertical gradient.</param>
    /// <param name="Edges">The outlines and shadows, in back-to-front order.</param>
    public sealed record FontAppearance(SKColor Color, SKColor BottomColor, IReadOnlyList<FontEdge> Edges)
    {
        // Sampled from assets/fonts at 9c20287, before the bitmap atlas removal. Keep the
        // palettes separate: selected buttons, pressed buttons and HUD numbers are different yellows.
        internal static FontAppearance ForStyle(string name)
        {
            FontEdge black = new(SKColors.Black, 2);
            return name switch
            {
                "AppFont" => Solid(0xffffff, black),
                "EditFntSelTit" => Solid(0xffffbb),
                "EditFntTit" => Solid(0xffff80),
                "EditFntTTip" or "MapStoryFont" or "PanelInstrBlackFnt" => Solid(0x000000),
                "MenuFntBtnNrm" => Solid(0xfebd2c, black),
                "MenuFntBtnSel" or "MenuFntEncTitSel" => Solid(0xf5f532, black),
                "MenuFntBtnPrs" => Solid(0xb27d09, new FontEdge(new SKColor(0, 0, 0, 180), 3, 0, 1, 0.6f), black),
                "MenuFntEncTit" => Solid(0xffbe2d, new FontEdge(SKColors.Black, 3, 0, 1, 0.5f), black),
                "MenuFntEncTxt" => Solid(0xf5f532, black),
                "MenuFntMapTxt" => Solid(0xf5f532, new FontEdge(SKColors.Black, 3, 0, 1, 0.5f), black),
                "MenuFntTit" => new FontAppearance(Rgb(0xfee34f), Rgb(0xf9ad02),
                    [new FontEdge(SKColors.Black, 4, 1, 2, 0.5f), new FontEdge(Rgb(0x999999), 2, -1, -1)]),
                "MenuFontRegTxt" => Solid(0xffe000),
                "MenuFontToolTip" => Solid(0xffffff, new FontEdge(Rgb(0xfe521c), 1.5f, 0, 0, 0.6f)),
                "MenuTTipFont" => Solid(0x8745ab),
                "PanelDigFnt" or "PanelDigTextFnt" => Solid(0xf6e73d, black),
                "PanelDigScr" => Solid(0xf6e73d, new FontEdge(SKColors.Black, 6)),
                "PanelDigTextRedFnt" => Solid(0xf63d3d, black),
                "GameFontScr" => new FontAppearance(Rgb(0xffff00), Rgb(0xffcc00),
                    [new FontEdge(SKColors.Black, 8), new FontEdge(Rgb(0xb06bde), 3, 1.5f, 1),
                     new FontEdge(Rgb(0x31ad00), 3, -1.5f, -1), new FontEdge(SKColors.Black, 1.5f)]),
                "VersionFont" => Solid(0xebebeb),
                _ => Solid(0xf6e73d, black),
            };
        }

        private static FontAppearance Solid(uint rgb, params FontEdge[] edges)
        {
            return new FontAppearance(Rgb(rgb), Rgb(rgb), edges);
        }

        private static SKColor Rgb(uint rgb)
        {
            return new SKColor(0xff000000 | rgb);
        }
    }
}
