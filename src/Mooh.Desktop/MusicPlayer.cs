using System;

using Mooh.Desktop.Platform.Audio;
using Mooh.Presentation.Music;

namespace Mooh.Desktop
{
    /// <summary>
    /// Plays a <c>Music.json</c> track: the tracker module rendered by the ported Skale replay engine
    /// into an audio feed kept a little ahead of playback.
    /// </summary>
    internal sealed class MusicPlayer : IDisposable
    {
        private const int Rate = 44100;
        private const int AheadFrames = Rate / 4;
        private const int ChunkFrames = 1024;

        private readonly IAudioFeed _feed;
        private readonly SkalePlayer _player;
        private double _trackVolume = 1;
        private readonly float[] _chunk = new float[ChunkFrames * 2];

        private MusicPlayer(IAudioFeed feed, SkalePlayer player)
        {
            _feed = feed;
            _player = player;
        }

        /// <summary>Starts a track.</summary>
        /// <param name="output">The audio output.</param>
        /// <param name="module">The cached module with decoded samples.</param>
        /// <param name="track">The cue's start position and gain.</param>
        /// <param name="volume">The user's music gain, applied before playback is queued.</param>
        /// <returns>The playing music, or <see langword="null"/> when there is no output.</returns>
        public static MusicPlayer? Start(IAudioOutput output, SkaleModule module, MusicTrack track, double volume = 1)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(module);
            ArgumentNullException.ThrowIfNull(track);
            IAudioFeed? feed = output.OpenFeed(Rate);
            if (feed is null)
            {
                return null;
            }

            MusicPlayer music = new(feed, new SkalePlayer(module, Rate, track.Position))
            {
                _trackVolume = track.Volume,
                Volume = volume,
            };
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

        /// <summary>Sets the user's music gain without restarting the tracker.</summary>
        public double Volume
        {
            set => _feed.Volume = (float)(_trackVolume * Math.Clamp(value, 0, 1));
        }

        /// <summary>Stops the music.</summary>
        public void Dispose()
        {
            _feed.Dispose();
        }
    }
}
