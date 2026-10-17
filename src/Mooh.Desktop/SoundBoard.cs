using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Desktop.Platform.Audio;
using Mooh.Presentation.Audio;
using Mooh.Presentation.Scene;

namespace Mooh.Desktop
{
    /// <summary>
    /// Plays the presenter's sound cues as the original's <c>soundCash.ObjSoundPlay</c> does: one
    /// of the alias's files at a volume and rate inside its ranges, panned by where it happens on
    /// screen, with no more of the alias at once than its buffer count.
    /// </summary>
    /// <param name="catalog">The <c>Sound.json</c> catalog.</param>
    /// <param name="output">The audio output.</param>
    /// <param name="soundsDirectory">The folder holding the <c>.ogg</c> files.</param>
    /// <param name="random">The source of the file, volume and rate choices.</param>
    internal sealed class SoundBoard(SoundCatalog catalog, IAudioOutput output, string soundsDirectory, Random random)
    {
        /// <summary>Gets or sets the user's sound-effect gain.</summary>
        public double Volume { get; set; } = 1;
        private readonly Dictionary<string, string> _paths = Directory.Exists(soundsDirectory)
            ? Directory.EnumerateFiles(soundsDirectory, "*.ogg").ToDictionary(
                path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            : [with(StringComparer.OrdinalIgnoreCase)];
        private readonly Dictionary<string, object?> _loaded = [with(StringComparer.Ordinal)];
        private readonly Dictionary<string, List<IVoice>> _playing = [with(StringComparer.Ordinal)];

        /// <summary>Plays a frame's cues.</summary>
        /// <param name="cues">The cues.</param>
        /// <param name="camera">The camera, which places each cue on the screen.</param>
        public void Play(IReadOnlyList<SoundCue> cues, SceneCamera camera)
        {
            ArgumentNullException.ThrowIfNull(cues);
            ArgumentNullException.ThrowIfNull(camera);
            foreach (SoundCue cue in cues)
            {
                (double x, double y) = camera.ToScreen(cue.X, cue.Y);
                Play(cue.Alias, cue.Centered ? SoundPlacement.Centered : SoundPlacement.At(x, y));
            }
        }

        /// <summary>Plays an alias.</summary>
        /// <param name="alias">The alias.</param>
        /// <param name="placement">Where it plays.</param>
        public void Play(string alias, SoundPlacement placement)
        {
            if (!catalog.Contains(alias))
            {
                return;
            }

            SoundDefinition sound = catalog[alias];
            if (sound.Files.Count == 0)
            {
                return;
            }

            if (!_playing.TryGetValue(alias, out List<IVoice>? voices))
            {
                voices = [];
                _playing[alias] = voices;
            }

            _ = voices.RemoveAll(voice => !voice.IsPlaying);
            if (voices.Count >= sound.Buffers)
            {
                return;
            }

            string file = sound.Files[random.Next(sound.Files.Count)];
            if (!_loaded.TryGetValue(file, out object? loaded))
            {
                string path = _paths.TryGetValue(file, out string? actual)
                    ? actual : Path.Combine(soundsDirectory, file + ".ogg");
                loaded = output.Load(path);
                _loaded[file] = loaded;
            }

            if (loaded is null)
            {
                return;
            }

            float volume = (float)(Between(sound.Volume) * Math.Clamp(Volume, 0, 1));
            voices.Add(output.Play(loaded, (float)placement.Left * volume, (float)placement.Right * volume, (float)Between(sound.Frequency)));
        }

        private double Between((double Min, double Max) range)
        {
            return range.Min + (random.NextDouble() * (range.Max - range.Min));
        }
    }
}
