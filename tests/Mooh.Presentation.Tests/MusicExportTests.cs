using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Presentation.Music;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Offline exports retain exact order boundaries across render block sizes.</summary>
    public sealed class MusicExportTests
    {
        /// <summary>Order timestamps count consumed stereo frames, independently of buffer sizes.</summary>
        [Fact]
        public void OrderTimestampsDoNotDependOnRenderBlockSize()
        {
            SkaleModule module = SkaleModule.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "music", "TrackMenu.skm")));
            List<(int Order, long Frame)> Render(int chunk)
            {
                SkalePlayer player = new(module, 44100, 0);
                List<(int, long)> events = [];
                player.OrderStarted += (order, frame) => events.Add((order, frame));
                float[] buffer = new float[chunk * 2];
                for (int at = 0; at < 44100 * 8; at += chunk)
                {
                    player.Render(buffer.AsSpan(0, Math.Min(chunk, (44100 * 8) - at) * 2));
                }
                return events;
            }
            List<(int Order, long Frame)> expected = Render(4096);
            Assert.True(expected.Count > 1);
            Assert.Equal((0, 0L), expected[0]);
            Assert.Equal(expected, Render(127));
            Assert.True(expected[1].Frame > 0);
        }
    }
}
