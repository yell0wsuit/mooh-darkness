using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Mooh.Desktop.Platform.Audio;

using SDL3;

using Xunit;

namespace Mooh.Desktop.Platform.Tests
{
    /// <summary>Exercises native mixer playback through SDL's hardware-independent audio driver.</summary>
    [Collection("Native audio")]
    public sealed class SdlAudioTests
    {
        /// <summary>A temporarily empty music stream must consume audio written afterward.</summary>
        [Fact]
        public void AFeedContinuesAfterRunningDry()
        {
            using SdlAudio audio = Open();
            using IAudioFeed feed = Assert.IsAssignableFrom<IAudioFeed>(audio.OpenFeed(44100));
            Thread.Sleep(100);
            feed.Write(new float[16384]);
            Assert.True(WaitUntil(() => feed.QueuedFrames == 0), "The music track stopped consuming its stream after an underrun.");
            Thread.Sleep(100);
            feed.Write(new float[16384]);
            Assert.True(WaitUntil(() => feed.QueuedFrames == 0), "The music track did not recover from a second underrun.");
        }

        /// <summary>Reusing a native track must not revive an earlier playback's voice.</summary>
        [Fact]
        public void AFinishedVoiceStaysFinishedWhenItsTrackIsReused()
        {
            using SdlAudio audio = Open();
            string path = Path.Combine(Path.GetTempPath(), $"mooh-audio-{Guid.NewGuid():N}.wav");
            try
            {
                WriteWave(path, 441);
                object shortSound = Assert.IsType<object>(audio.Load(path), exactMatch: false);
                IVoice finished = audio.Play(shortSound, 1, 1, 1);
                Assert.True(WaitUntil(() => !finished.IsPlaying));
                WriteWave(path, 44100);
                object longSound = Assert.IsType<object>(audio.Load(path), exactMatch: false);
                IVoice current = audio.Play(longSound, 1, 1, 1);
                Assert.True(current.IsPlaying);
                Assert.False(finished.IsPlaying);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static SdlAudio Open()
        {
            Assert.True(SDL.SetHint("SDL_AUDIO_DRIVER", "dummy"));
            SdlAudio? audio = SdlAudio.TryOpen(out string? error);
            Assert.True(audio is not null, error);
            return audio;
        }

        private static bool WaitUntil(Func<bool> condition)
        {
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.Elapsed < TimeSpan.FromSeconds(3))
            {
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(5);
            }

            return condition();
        }

        private static void WriteWave(string path, int frames)
        {
            using BinaryWriter writer = new(File.Create(path));
            writer.Write("RIFF"u8);
            writer.Write(36 + (frames * 2));
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(44100);
            writer.Write(88200);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(frames * 2);
            for (int frame = 0; frame < frames; frame++)
            {
                writer.Write((short)(Math.Sin(frame * 0.1) * 10000));
            }
        }
    }

    /// <summary>Serializes tests that initialize and shut down SDL's audio subsystem.</summary>
    [CollectionDefinition("Native audio", DisableParallelization = true)]
    public sealed class NativeAudioTestsGroup;
}
