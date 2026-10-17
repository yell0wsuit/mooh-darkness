using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Presentation.Sprites;

using SkiaSharp;

namespace Mooh.Desktop
{
    /// <summary>
    /// The solid pixels of the recovered atlases, for the pickup sparks. A pixel counts as solid
    /// from half opacity up, standing in for the 1-bit mask the original builds at load time.
    /// </summary>
    /// <param name="spritesDirectory">The folder holding the <c>Sprite*.png</c> atlases and their <c>.json</c> tables.</param>
    internal sealed class SpriteMasks(string spritesDirectory) : ISpriteMask, IDisposable
    {
        private const byte SolidAlpha = 128;

        private readonly Dictionary<string, (SpritePage Frames, SKBitmap Pixels)?> _pages = [with(StringComparer.Ordinal)];

        /// <inheritdoc/>
        public SpriteFrame? FrameOf(string page, string frame)
        {
            return PageOf(page) is (SpritePage frames, _) && frames.Contains(frame) ? frames[frame] : null;
        }

        /// <inheritdoc/>
        public bool IsSolid(string page, int x, int y)
        {
            return PageOf(page) is (_, SKBitmap pixels)
                && x >= 0 && y >= 0 && x < pixels.Width && y < pixels.Height
                && pixels.GetPixel(x, y).Alpha >= SolidAlpha;
        }

        /// <summary>Releases the decoded atlases.</summary>
        public void Dispose()
        {
            foreach ((SpritePage, SKBitmap Pixels)? page in _pages.Values)
            {
                page?.Pixels.Dispose();
            }

            _pages.Clear();
        }

        private (SpritePage Frames, SKBitmap Pixels)? PageOf(string page)
        {
            if (_pages.TryGetValue(page, out (SpritePage, SKBitmap)? loaded))
            {
                return loaded;
            }

            string table = Path.Combine(spritesDirectory, page + ".json");
            string image = Path.Combine(spritesDirectory, page + ".png");
            (SpritePage, SKBitmap)? found = null;
            if (File.Exists(table) && File.Exists(image) && SKBitmap.Decode(image) is SKBitmap pixels)
            {
                using FileStream stream = File.OpenRead(table);
                found = (SpritePage.Load(page, stream), pixels);
            }

            _pages[page] = found;
            return found;
        }
    }
}
