using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// The recovered level backdrop, <c>GameBack.CBackLayer</c>: the <c>BackA_1_01</c> image, a
    /// layer of random 250 by 220 tiles in the level's style, and lamps glowing on a quarter of
    /// those tiles. Both layers scroll slower than the world.
    /// </summary>
    /// <remarks>
    /// <c>RTCalc</c> lets the image move at 0.5 of the world at most, and less on a level too wide
    /// or tall for its overscan to last; the tile layer moves at 0.75 and is laid out over the
    /// level plus one screen. <c>ModRender</c> draws each layer at the camera's offset from the
    /// screen center times its factor, truncated, and the lamps at the tile layer's position.
    /// </remarks>
    public sealed class Backdrop
    {
        /// <summary>The shared backdrop image, <c>BackA_1_01</c>.</summary>
        public const string ImageName = "BackA_1_01";

        /// <summary>The lamps' page, <c>SpriteBackLamp</c>.</summary>
        public const string LampPage = "SpriteBackLamp";

        private const double ScreenWidth = 800;
        private const double ScreenHeight = 600;
        private const double ImageParallax = 0.5;
        private const double TileParallax = 0.75;
        private const int TileWidth = 250;
        private const int TileHeight = 220;

        /// <summary><c>CNT_LampCellProb</c>, out of the recovered random source's 32768.</summary>
        private const int LampProbability = 4096 * 2;

        private static readonly string[] LampFrames = [.. Enumerable.Range(1, 18).Select(index => $"OLamp{index:00}")];
        private static readonly string[] GlowFrames = [.. Enumerable.Range(0, 12).Select(index => $"GLamp{index * 2:00}")];

        private readonly double _imageFactorX;
        private readonly double _imageFactorY;
        private readonly string _tilePage;
        private readonly List<(string Frame, int X, int Y)> _tiles = [];
        private readonly List<Lamp> _lamps = [];

        /// <summary>Lays out a level's backdrop.</summary>
        /// <param name="style">The level's style.</param>
        /// <param name="widthCells">The level's width in cells.</param>
        /// <param name="heightCells">The level's height in cells.</param>
        /// <param name="imageSize">The <c>BackA_1_01</c> image's size in pixels.</param>
        /// <param name="random">The source of the tile and lamp choices.</param>
        /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
        public Backdrop(BackdropStyle style, int widthCells, int heightCells, (int Width, int Height) imageSize, Random random)
        {
            ArgumentNullException.ThrowIfNull(random);
            double width = Math.Max(1, widthCells) * SceneCamera.CellWidth;
            double height = Math.Max(1, heightCells) * SceneCamera.CellHeight;
            _imageFactorX = Math.Min(ImageParallax, (imageSize.Width - ScreenWidth) / width);
            _imageFactorY = Math.Min(ImageParallax, (imageSize.Height - ScreenHeight) / height);
            (_tilePage, string prefix, int count) = style switch
            {
                BackdropStyle.Tropical => ("SpriteBackTrop", "BackTropical_2_", 7),
                BackdropStyle.Desert => ("SpriteBackDesert", "BackDesert_2_", 7),
                BackdropStyle.Normal => ("SpriteBack", "BackNrm_2_", 6),
                _ => ("SpriteBack", "BackNrm_2_", 6),
            };
            int columns = (int)Math.Ceiling((width + ScreenWidth) / TileWidth);
            int rows = (int)Math.Ceiling((height + ScreenHeight) / TileHeight);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    _tiles.Add(($"{prefix}{random.Next(count) + 1:00}", column * TileWidth, row * TileHeight));
                }
            }

            // ModLampsInstall: a lamp on a quarter of the tiles, somewhere in each one's middle half.
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (random.Next(32768) > LampProbability)
                    {
                        continue;
                    }

                    double x = (column * TileWidth) + (TileWidth * (0.25 + (random.NextDouble() * 0.5)));
                    double y = (row * TileHeight) + (TileHeight * (0.25 + (random.NextDouble() * 0.5)));
                    double fast = 1.0 / 50;
                    double slow = 1.0 / 75;
                    _lamps.Add(new Lamp(x, y, 1 / (fast + (random.NextDouble() * (slow - fast)))));
                }
            }
        }

        /// <summary>Picks a level's style as <c>LvlBackType</c> does: the most common vote wins.</summary>
        /// <param name="archivalTypes">The level's object types.</param>
        /// <param name="library">The recovered registrations.</param>
        /// <returns>The style, or <see cref="BackdropStyle.Normal"/> when nothing votes.</returns>
        public static BackdropStyle StyleOf(IEnumerable<string> archivalTypes, AnimationLibrary library)
        {
            ArgumentNullException.ThrowIfNull(archivalTypes);
            ArgumentNullException.ThrowIfNull(library);
            return archivalTypes
                .Select(type => library.TryGetLook(type, out ObjectLook look) ? look.Backdrop : null)
                .OfType<BackdropStyle>()
                .GroupBy(style => style)
                .OrderByDescending(group => group.Count())
                .Select(group => (BackdropStyle?)group.Key)
                .FirstOrDefault() ?? BackdropStyle.Normal;
        }

        /// <summary>Plays the lamps.</summary>
        /// <param name="seconds">The elapsed time.</param>
        public void Advance(double seconds)
        {
            foreach (Lamp lamp in _lamps)
            {
                lamp.Position = (lamp.Position + (seconds * 1000 / lamp.FrameMilliseconds)) % LampFrames.Length;
            }
        }

        /// <summary>Places the layers for the camera.</summary>
        /// <param name="baseX">The camera's world base, <c>lScrX</c>.</param>
        /// <param name="baseY">The camera's world base, <c>lScrY</c>.</param>
        /// <returns>Where the image goes and the sprites to draw over it, in order.</returns>
        public BackdropLayout Layout(double baseX, double baseY)
        {
            double offsetX = baseX - (ScreenWidth / 2);
            double offsetY = baseY - (ScreenHeight / 2);
            int tileX = (int)(offsetX * TileParallax);
            int tileY = (int)(offsetY * TileParallax);
            List<BackdropSprite> sprites = [.. _tiles.Select(tile => new BackdropSprite(_tilePage, tile.Frame, tileX + tile.X, tileY + tile.Y, false))];
            foreach (Lamp lamp in _lamps)
            {
                int frame = (int)lamp.Position;
                sprites.Add(new BackdropSprite(LampPage, LampFrames[frame], tileX + lamp.X, tileY + lamp.Y, false));

                // The glow, AnmBackLampFx, is drawn additively over the lamp at the lamp's phase
                // scaled to its length: the native plain render's DInds[(int)(fAInd * len(D) / len(A)) % len(D)].
                int glow = (int)(lamp.Position * GlowFrames.Length / LampFrames.Length) % GlowFrames.Length;
                sprites.Add(new BackdropSprite(LampPage, GlowFrames[glow], tileX + lamp.X, tileY + lamp.Y, true));
            }

            return new BackdropLayout((int)(offsetX * _imageFactorX), (int)(offsetY * _imageFactorY), sprites);
        }

        private sealed class Lamp(double x, double y, double frameMilliseconds)
        {
            public double X { get; } = x;

            public double Y { get; } = y;

            public double FrameMilliseconds { get; } = frameMilliseconds;

            public double Position
            {
                get; set;
            }
        }
    }

    /// <summary>A backdrop placed for one frame.</summary>
    /// <param name="ImageX">Where the <c>BackA_1_01</c> image's left edge goes on the screen.</param>
    /// <param name="ImageY">Where the image's top edge goes.</param>
    /// <param name="Sprites">The tiles, lamps and glows to draw over it, in order.</param>
    public sealed record BackdropLayout(int ImageX, int ImageY, IReadOnlyList<BackdropSprite> Sprites);

    /// <summary>One backdrop frame, anchored on the screen.</summary>
    /// <param name="Page">The sprite page.</param>
    /// <param name="Frame">The frame name.</param>
    /// <param name="X">The anchor's screen column in pixels.</param>
    /// <param name="Y">The anchor's screen row in pixels.</param>
    /// <param name="Additive">Whether the frame is drawn additively, as the lamps' glows are.</param>
    public readonly record struct BackdropSprite(string Page, string Frame, double X, double Y, bool Additive);
}
