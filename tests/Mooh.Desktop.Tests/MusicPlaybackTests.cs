using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Desktop.Platform.Audio;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Checks music gain and screen cue transitions at the streaming boundary.</summary>
    public sealed class MusicPlaybackTests
    {
        /// <summary>The initial queued audio already uses the user's volume, including mute.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(0.25)]
        public void TrackStartsAtTheConfiguredVolume(double volume)
        {
            TestAudio output = new();
            using MusicDirector director = new(output, Path.Combine(AppContext.BaseDirectory, "assets", "music"));
            director.Update("menu", volume);
            Assert.NotEmpty(output.Feeds[0].WriteVolumes);
            Assert.All(output.Feeds[0].WriteVolumes, gain => Assert.Equal((float)(0.6 * volume), gain));
        }

        /// <summary>Repeated cues and volume changes preserve the playing tracker.</summary>
        [Fact]
        public void SameCueContinuesAndChangedCueStopsThePreviousFeed()
        {
            TestAudio output = new();
            using MusicDirector director = new(output, Path.Combine(AppContext.BaseDirectory, "assets", "music"));
            director.Update("menu", 1);
            director.Update("menu", 0.5);
            _ = Assert.Single(output.Feeds);
            Assert.False(output.Feeds[0].Disposed);
            director.Update("gameend", 0.5);
            Assert.Equal(2, output.Feeds.Count);
            Assert.True(output.Feeds[0].Disposed);
            Assert.Equal(0.325f, output.Feeds[1].Volume);
        }

        /// <summary>Screen cues sharing a module reuse its decoded samples, even after playback stops.</summary>
        [Fact]
        public void CueChangesDecodeTheSharedModuleOnlyOnce()
        {
            TestAudio output = new();
            using MusicDirector director = new(output, Path.Combine(AppContext.BaseDirectory, "assets", "music"));
            director.Update("menu", 1);
            int decoded = output.DecodeCalls;
            Assert.True(decoded > 0);

            director.Update("game", 1);
            director.Update("gameend", 1);
            director.Update(string.Empty, 1);
            director.Update("menu", 1);

            Assert.Equal(decoded, output.DecodeCalls);
            Assert.Equal(4, output.Feeds.Count);
            Assert.All(output.Feeds.GetRange(0, 3), feed => Assert.True(feed.Disposed));
            Assert.False(output.Feeds[3].Disposed);
        }

        /// <summary>A fresh tracker restarts a cue identically without inheriting another cue's state.</summary>
        [Fact]
        public void CachedSamplesPreserveRestartedCueOutput()
        {
            TestAudio output = new()
            {
                DecodeSamples = true
            };
            using MusicDirector director = new(output, Path.Combine(AppContext.BaseDirectory, "assets", "music"));
            director.Update("menu", 1);
            float[] original = [.. output.Feeds[0].Samples];
            Assert.Contains(original, value => value != 0);
            int decoded = output.DecodeCalls;
            director.Update("game", 1);
            director.Update("menu", 1);

            Assert.Equal(original, output.Feeds[2].Samples);
            Assert.Equal(decoded, output.DecodeCalls);
        }

        private sealed class TestAudio : IAudioOutput
        {
            public List<TestFeed> Feeds { get; } = [];
            public bool DecodeSamples
            {
                get; init;
            }
            public int DecodeCalls
            {
                get; private set;
            }
            public object? Load(string path)
            {
                return null;
            }
            public IVoice Play(object sound, float left, float right, float rate)
            {
                throw new NotSupportedException();
            }
            public (float[] Samples, int Rate)? DecodeMono(byte[] encoded)
            {
                DecodeCalls++;
                if (!DecodeSamples)
                {
                    return null;
                }

                float[] pcm = new float[128];
                for (int i = 0; i < pcm.Length; i++)
                {
                    pcm[i] = MathF.Sin(i * 0.1f);
                }
                return (pcm, 44100);
            }
            public IAudioFeed OpenFeed(int rate)
            {
                TestFeed feed = new();
                Feeds.Add(feed);
                return feed;
            }
            public void Dispose()
            {
            }
        }

        private sealed class TestFeed : IAudioFeed
        {
            public List<float> WriteVolumes { get; } = [];
            public List<float> Samples { get; } = [];
            public int QueuedFrames
            {
                get; private set;
            }
            public float Volume
            {
                get; set;
            }
            public bool Disposed
            {
                get; private set;
            }
            public void Write(ReadOnlySpan<float> interleaved)
            {
                WriteVolumes.Add(Volume);
                Samples.AddRange(interleaved.ToArray());
                QueuedFrames += interleaved.Length / 2;
            }
            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
