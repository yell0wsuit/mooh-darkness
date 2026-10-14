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

        /// <summary>Decodes an encoded sound, such as a tracker sample's Ogg payload, to mono samples.</summary>
        /// <param name="encoded">The encoded bytes.</param>
        /// <returns>The samples and their rate, or <see langword="null"/> when they could not be decoded.</returns>
        (float[] Samples, int Rate)? DecodeMono(byte[] encoded);

        /// <summary>Opens a stereo float stream a host keeps fed, such as rendered music.</summary>
        /// <param name="rate">The stream's rate.</param>
        /// <returns>The stream, or <see langword="null"/> when there is no output.</returns>
        IAudioFeed? OpenFeed(int rate);
    }

    /// <summary>A stereo float feed the host writes ahead of playback, such as rendered music.</summary>
    public interface IAudioFeed : IDisposable
    {
        /// <summary>Gets how many frames are queued and not yet played.</summary>
        int QueuedFrames
        {
            get;
        }

        /// <summary>Sets the stream's gain.</summary>
        float Volume
        {
            set;
        }

        /// <summary>Queues interleaved stereo frames.</summary>
        /// <param name="interleaved">The frames, left then right.</param>
        void Write(ReadOnlySpan<float> interleaved);
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
        public (float[] Samples, int Rate)? DecodeMono(byte[] encoded)
        {
            return null;
        }

        /// <inheritdoc/>
        public IAudioFeed? OpenFeed(int rate)
        {
            return null;
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
