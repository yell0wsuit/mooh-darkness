using System;

using Mooh.Desktop.Platform.Audio;

namespace Mooh.Desktop
{
    /// <summary>Switches the original menu, gameplay and game-end module positions on screen changes.</summary>
    internal sealed class MusicDirector(IAudioOutput audio, string directory) : IDisposable
    {
        private MusicPlayer? _playing;
        private string _track = string.Empty;

        public void Update(string track, double volume)
        {
            if (_track != track)
            {
                _playing?.Dispose();
                _track = track;
                _playing = track.Length == 0 ? null : MusicPlayer.Start(audio, directory, track, volume);
            }
            if (_playing is not null)
            {
                _playing.Volume = volume;
                _playing.Pump();
            }
        }

        public void Dispose()
        {
            _playing?.Dispose();
        }
    }
}
