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

        private sealed class TestAudio : IAudioOutput
        {
            public List<TestFeed> Feeds { get; } = [];
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
                return null;
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
                QueuedFrames += interleaved.Length / 2;
            }
            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
