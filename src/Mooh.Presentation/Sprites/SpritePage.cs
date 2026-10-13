using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mooh.Presentation.Sprites
{
    /// <summary>
    /// Reads a recovered sprite page frame table, the <c>.seq</c> file beside each <c>Sprite*.png</c>
    /// atlas.
    /// </summary>
    /// <remarks>
    /// The format is <c>anm2</c>, a version and the frame count, then one 20-byte record per frame:
    /// the name's offset and length, then the frame's left, top, right and bottom edges relative to
    /// its anchor and its atlas position, each a little-endian 16-bit value after the two 32-bit
    /// name fields. A <c>cst1</c> chunk and the name table follow. A frame whose four edges are all
    /// zero is empty and draws nothing.
    /// </remarks>
    public sealed class SpritePage
    {
        private const int HeaderSize = 12;
        private const int RecordSize = 20;

        private readonly Dictionary<string, SpriteFrame> _frames;

        private SpritePage(string name, Dictionary<string, SpriteFrame> frames)
        {
            Name = name;
            _frames = frames;
        }

        /// <summary>Gets the page name, such as <c>SpritePlayer</c>.</summary>
        public string Name
        {
            get;
        }

        /// <summary>Gets the number of frames.</summary>
        public int Count => _frames.Count;

        /// <summary>Gets every frame.</summary>
        public IEnumerable<SpriteFrame> Frames => _frames.Values;

        /// <summary>Gets a frame by name.</summary>
        /// <param name="frameName">The frame name.</param>
        /// <returns>The frame.</returns>
        /// <exception cref="KeyNotFoundException">The page has no such frame.</exception>
        public SpriteFrame this[string frameName] => _frames[frameName];

        /// <summary>Gets whether the page has a frame.</summary>
        /// <param name="frameName">The frame name.</param>
        /// <returns><see langword="true"/> when the frame exists.</returns>
        public bool Contains(string frameName)
        {
            return _frames.ContainsKey(frameName);
        }

        /// <summary>Reads a page's frame table.</summary>
        /// <param name="name">The page name.</param>
        /// <param name="stream">The <c>.seq</c> bytes.</param>
        /// <returns>The page.</returns>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="InvalidDataException">The data is not a recovered frame table.</exception>
        public static SpritePage Load(string name, Stream stream)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(stream);
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            ReadOnlySpan<byte> data = copy.GetBuffer().AsSpan(0, (int)copy.Length);
            if (data.Length < HeaderSize || !data[..4].SequenceEqual("anm2"u8))
            {
                throw new InvalidDataException($"Sprite page '{name}' is not an anm2 frame table.");
            }

            int count = BinaryPrimitives.ReadInt32LittleEndian(data[8..]);
            if (count < 0 || HeaderSize + ((long)count * RecordSize) > data.Length)
            {
                throw new InvalidDataException($"Sprite page '{name}' declares more frames than it holds.");
            }

            Dictionary<string, SpriteFrame> frames = new(count, StringComparer.Ordinal);
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> record = data.Slice(HeaderSize + (index * RecordSize), RecordSize);
                int nameOffset = BinaryPrimitives.ReadInt32LittleEndian(record);
                int nameLength = BinaryPrimitives.ReadInt32LittleEndian(record[4..]);
                if (nameOffset < 0 || nameLength < 0 || (long)nameOffset + nameLength > data.Length)
                {
                    throw new InvalidDataException($"Sprite page '{name}' has a frame name outside its data.");
                }

                string frameName = Encoding.Latin1.GetString(data.Slice(nameOffset, nameLength));
                int left = BinaryPrimitives.ReadInt16LittleEndian(record[8..]);
                int top = BinaryPrimitives.ReadInt16LittleEndian(record[10..]);
                int right = BinaryPrimitives.ReadInt16LittleEndian(record[12..]);
                int bottom = BinaryPrimitives.ReadInt16LittleEndian(record[14..]);
                frames[frameName] = new SpriteFrame(
                    frameName,
                    BinaryPrimitives.ReadInt16LittleEndian(record[16..]),
                    BinaryPrimitives.ReadInt16LittleEndian(record[18..]),
                    right - left,
                    bottom - top,
                    left,
                    top);
            }

            return new SpritePage(name, frames);
        }
    }
}
