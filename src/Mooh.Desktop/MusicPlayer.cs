using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Desktop.Platform.Audio;
using Mooh.Presentation.Music;

namespace Mooh.Desktop
{
    /// <summary>
    /// Plays a <c>Music.ini</c> track: the tracker module rendered by the ported Skale replay engine
    /// into an audio feed kept a little ahead of playback.
    /// </summary>
    internal sealed class MusicPlayer : IDisposable
    {
        private const int Rate = 44100;
        private const int AheadFrames = Rate / 4;
        private const int ChunkFrames = 1024;

        private readonly IAudioFeed _feed;
        private readonly SkalePlayer _player;
        private readonly float[] _chunk = new float[ChunkFrames * 2];

        private MusicPlayer(IAudioFeed feed, SkalePlayer player)
        {
            _feed = feed;
            _player = player;
        }

        /// <summary>Starts a track.</summary>
        /// <param name="output">The audio output.</param>
        /// <param name="musicDirectory">The folder holding <c>Music.ini</c> and the modules.</param>
        /// <param name="trackName">The track, such as <c>game</c>.</param>
        /// <returns>The playing music, or <see langword="null"/> when there is no output or no such track.</returns>
        public static MusicPlayer? Start(IAudioOutput output, string musicDirectory, string trackName)
        {
            ArgumentNullException.ThrowIfNull(output);
            IReadOnlyDictionary<string, MusicTrack> tracks;
            using (FileStream stream = File.OpenRead(Path.Combine(musicDirectory, "Music.ini")))
            {
                tracks = MusicCatalog.Load(stream);
            }

            if (!tracks.TryGetValue(trackName, out MusicTrack? track))
            {
                return null;
            }

            SkaleModule module = SkaleModule.Load(File.ReadAllBytes(Path.Combine(musicDirectory, track.File + ".skm")));
            foreach (SkaleSample sample in module.Instruments.Values.SelectMany(instrument => instrument.Samples.Values))
            {
                if (output.DecodeMono(sample.Ogg) is (float[] decoded, _))
                {
                    sample.SetDecoded(decoded);
                }
            }

            IAudioFeed? feed = output.OpenFeed(Rate);
            if (feed is null)
            {
                return null;
            }

            feed.Volume = (float)track.Volume;
            MusicPlayer music = new(feed, new SkalePlayer(module, Rate, track.Position));
            music.Pump();
            return music;
        }

        /// <summary>Renders enough to stay a quarter of a second ahead of playback.</summary>
        public void Pump()
        {
            while (_feed.QueuedFrames < AheadFrames)
            {
                _player.Render(_chunk);
                _feed.Write(_chunk);
            }
        }

        /// <summary>Stops the music.</summary>
        public void Dispose()
        {
            _feed.Dispose();
        }
    }
}
