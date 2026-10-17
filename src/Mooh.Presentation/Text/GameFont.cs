using System;
using System.IO;

using SkiaSharp;

namespace Mooh.Presentation.Text
{
    /// <summary>A Comic Neue face measured and drawn with the same cached Skia font, following Contre Jour DX.</summary>
    public sealed class GameFont : IDisposable
    {
        private readonly SKTypeface _typeface;

        private GameFont(string name, SKTypeface typeface)
        {
            Name = name;
            Appearance = FontAppearance.ForStyle(name);
            _typeface = typeface;
            Font = new SKFont(typeface, 100) { Edging = SKFontEdging.Antialias };
            Font.Size = CapHeight(name) * 100 / Font.Metrics.CapHeight;
        }

        /// <summary>Gets the UI style role.</summary>
        public string Name
        {
            get;
        }

        /// <summary>Gets the font used for both layout and rendering.</summary>
        public SKFont Font
        {
            get;
        }

        /// <summary>Gets the baseline adjustment for roles positioned relative to a panel top.</summary>
        public float BaselineOffset => Name switch
        {
            "PanelDigTextFnt" or "PanelDigTextRedFnt" => 13,
            "GameFontScr" => 36,
            _ => 0,
        };

        /// <summary>Gets the original bitmap style's palette, outlines and shadows.</summary>
        public FontAppearance Appearance
        {
            get;
        }

        /// <summary>Gets the solid fill or top gradient color.</summary>
        public SKColor Color => Appearance.Color;

        /// <summary>Gets the bundled TrueType filename for a style.</summary>
        public static string FileName(string name)
        {
            return name is "MapStoryFont" or "MenuFontToolTip" or "MenuTTipFont" or "MenuFntEncTxt" or "MenuFontRegTxt" or "PanelInstrBlackFnt"
                ? "ComicNeue-Regular.ttf" : "ComicNeue-Bold.ttf";
        }

        /// <summary>Loads a TrueType face for a named UI style.</summary>
        public static GameFont Load(string name, Stream stream)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(stream);
            SKTypeface typeface = SKTypeface.FromStream(stream)
                ?? throw new InvalidDataException("The Comic Neue TrueType font could not be decoded.");
            return new GameFont(name, typeface);
        }

        /// <summary>Measures a line's advance in logical screen pixels.</summary>
        public double Width(string text)
        {
            return Font.MeasureText(text);
        }

        /// <summary>Gets whether a visible character has a glyph.</summary>
        public bool Has(char character)
        {
            return !char.IsWhiteSpace(character) && !char.IsControl(character) && Font.ContainsGlyph(character);
        }

        /// <summary>Gets the advance, including spacing characters.</summary>
        public (double X, double Y) Advance(char character)
        {
            return (Width(character.ToString()), 0);
        }

        /// <summary>Measures the visible bounds relative to the baseline.</summary>
        public (int Left, int Top, int Right, int Bottom) Bounds(string text)
        {
            _ = Font.MeasureText(text, out SKRect bounds);
            return ((int)Math.Floor(bounds.Left), (int)Math.Floor(bounds.Top + BaselineOffset),
                (int)Math.Ceiling(bounds.Right), (int)Math.Ceiling(bounds.Bottom + BaselineOffset));
        }

        /// <summary>Gets the pen position for aligned text.</summary>
        public double AlignedX(string text, double x, TextAlignment alignment)
        {
            return alignment switch
            {
                TextAlignment.Left => x,
                TextAlignment.Center => x - (Width(text) / 2),
                TextAlignment.Right => x - Width(text),
                _ => throw new ArgumentOutOfRangeException(nameof(alignment)),
            };
        }

        /// <summary>Releases cached native font resources.</summary>
        public void Dispose()
        {
            Font.Dispose();
            _typeface.Dispose();
        }

        private static float CapHeight(string name)
        {
            return name switch
            {
                "PanelDigScr" => 40,
                "MenuFntTit" => 42,
                "GameFontScr" => 34,
                "PanelDigFnt" => 20,
                "MenuFntBtnNrm" or "MenuFntBtnSel" or "MenuFntBtnPrs" or "MenuFntEncTit" or "MenuFntEncTitSel" => 23,
                "MenuFntMapTxt" => 15,
                "MenuFntEncTxt" => 14,
                "MenuFontToolTip" => 14,
                "VersionFont" or "EditFntTTip" or "EditFntSelTit" or "EditFntTit" => 9,
                _ => 12,
            };
        }
    }
}
