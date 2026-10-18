using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Desktop.Platform.Audio;
using Mooh.Presentation.Music;

namespace Mooh.Desktop
{
    /// <summary>Switches the original menu, gameplay and game-end module positions on screen changes.</summary>
    internal sealed class MusicDirector(IAudioOutput audio, string directory) : IDisposable
    {
        private readonly Dictionary<string, SkaleModule> _modules = [with(StringComparer.Ordinal)];
        private IReadOnlyDictionary<string, MusicTrack>? _tracks;
        private MusicPlayer? _playing;
        private string _track = string.Empty;

        public void Update(string track, double volume)
        {
            if (_track != track)
            {
                _playing?.Dispose();
                _track = track;
                _playing = track.Length == 0 ? null : Start(track, volume);
            }
            if (_playing is not null)
            {
                _playing.Volume = volume;
                _playing.Pump();
            }
        }

        private MusicPlayer? Start(string name, double volume)
        {
            ArgumentNullException.ThrowIfNull(audio);
            if (_tracks is null)
            {
                using FileStream stream = File.OpenRead(Path.Combine(directory, "Music.json"));
                _tracks = MusicCatalog.Load(stream);
            }

            if (!_tracks.TryGetValue(name, out MusicTrack? track))
            {
                return null;
            }

            if (!_modules.TryGetValue(track.File, out SkaleModule? module))
            {
                module = SkaleModule.Load(Path.Combine(directory, track.File + ".json"));
                foreach (SkaleSample sample in module.Instruments.Values.SelectMany(instrument => instrument.Samples.Values))
                {
                    if (audio.DecodeMono(sample.Ogg) is (float[] decoded, _))
                    {
                        sample.SetDecoded(decoded);
                    }
                }
                _modules.Add(track.File, module);
            }

            // Module/sample data is shared, but channels, envelopes and playback position
            // belong to a fresh tracker for every cue.
            return MusicPlayer.Start(audio, module, track, volume);
        }

        public void Dispose()
        {
            _playing?.Dispose();
            _playing = null;
            _modules.Clear();
            _tracks = null;
        }
    }
}
