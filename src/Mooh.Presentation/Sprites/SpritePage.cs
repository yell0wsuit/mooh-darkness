using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mooh.Presentation.Sprites
{
    /// <summary>Reads TexturePacker JSON Hash or Array atlases using Contre Jour DX's trim and pivot conventions.</summary>
    public sealed class SpritePage
    {
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
        /// <param name="stream">The TexturePacker JSON bytes.</param>
        /// <returns>The page.</returns>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="InvalidDataException">The atlas is malformed or uses unsupported rotation.</exception>
        public static SpritePage Load(string name, Stream stream)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(stream);
            try
            {
                using JsonDocument document = JsonDocument.Parse(stream);
                JsonElement entries = document.RootElement.GetProperty("frames");
                Dictionary<string, SpriteFrame> frames = [with(StringComparer.Ordinal)];
                if (entries.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty entry in entries.EnumerateObject())
                    {
                        Add(entry.Name, entry.Value);
                    }
                }
                else if (entries.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement entry in entries.EnumerateArray())
                    {
                        Add(entry.GetProperty("filename").GetString()!, entry);
                    }
                }
                else
                {
                    throw new InvalidDataException("Atlas frames must be an object or array.");
                }
                return new SpritePage(name, frames);

                void Add(string key, JsonElement entry)
                {
                    if (entry.TryGetProperty("rotated", out JsonElement rotated) && rotated.GetBoolean())
                    {
                        throw new InvalidDataException($"Atlas '{name}' frame '{key}' is rotated; disable rotation in TexturePacker.");
                    }
                    string frameName = key.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? key[..^4] : key;
                    JsonElement rect = entry.GetProperty("frame");
                    int x = rect.GetProperty("x").GetInt32();
                    int y = rect.GetProperty("y").GetInt32();
                    int width = rect.GetProperty("w").GetInt32();
                    int height = rect.GetProperty("h").GetInt32();
                    int offsetX = 0, offsetY = 0;
                    if (entry.TryGetProperty("spriteSourceSize", out JsonElement offset))
                    {
                        offsetX = offset.GetProperty("x").GetInt32();
                        offsetY = offset.GetProperty("y").GetInt32();
                    }
                    int sourceWidth = width, sourceHeight = height;
                    if (entry.TryGetProperty("sourceSize", out JsonElement source))
                    {
                        sourceWidth = source.GetProperty("w").GetInt32();
                        sourceHeight = source.GetProperty("h").GetInt32();
                    }
                    double pivotX = 0.5, pivotY = 0.5;
                    if (entry.TryGetProperty("pivot", out JsonElement pivot))
                    {
                        pivotX = pivot.GetProperty("x").GetDouble();
                        pivotY = pivot.GetProperty("y").GetDouble();
                    }
                    if (x < 0 || y < 0 || width < 0 || height < 0 || sourceWidth < 0 || sourceHeight < 0
                        || !double.IsFinite(pivotX) || !double.IsFinite(pivotY))
                    {
                        throw new InvalidDataException($"Atlas '{name}' frame '{key}' has invalid geometry.");
                    }
                    SpriteFrame frame = new(frameName, x, y, width, height,
                        checked((int)Math.Round(offsetX - (pivotX * sourceWidth))),
                        checked((int)Math.Round(offsetY - (pivotY * sourceHeight))));
                    if (!frames.TryAdd(frameName, frame))
                    {
                        throw new InvalidDataException($"Atlas '{name}' repeats frame '{frameName}'.");
                    }
                }
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
            {
                throw new InvalidDataException($"Atlas '{name}' contains invalid TexturePacker JSON.", error);
            }
        }
    }
}
