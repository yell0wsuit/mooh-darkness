using System;

namespace Mooh.Desktop.Platform.Audio
{
    /// <summary>Plays sound effects.</summary>
    public interface IAudioOutput : IDisposable
    {
        /// <summary>Loads a sound file once, decoded.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The loaded sound, or <see langword="null"/> when it could not be loaded.</returns>
        object? Load(string path);

        /// <summary>Plays a loaded sound once.</summary>
        /// <param name="sound">A sound <see cref="Load"/> returned.</param>
        /// <param name="left">The left gain.</param>
        /// <param name="right">The right gain.</param>
        /// <param name="rate">The playback rate, 1 for the recorded pitch.</param>
        /// <returns>The voice playing it, to ask whether it still plays.</returns>
        IVoice Play(object sound, float left, float right, float rate);
    }

    /// <summary>One sound playing.</summary>
    public interface IVoice
    {
        /// <summary>Gets whether the sound is still playing.</summary>
        bool IsPlaying
        {
            get;
        }
    }

    /// <summary>The audio of a machine without a playback device: everything is silent.</summary>
    public sealed class SilentAudio : IAudioOutput
    {
        private static readonly SilentVoice Finished = new();

        /// <inheritdoc/>
        public object? Load(string path)
        {
            return null;
        }

        /// <inheritdoc/>
        public IVoice Play(object sound, float left, float right, float rate)
        {
            return Finished;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        private sealed class SilentVoice : IVoice
        {
            public bool IsPlaying => false;
        }
    }
}
