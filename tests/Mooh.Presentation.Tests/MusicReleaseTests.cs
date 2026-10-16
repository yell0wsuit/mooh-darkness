using System;
using System.IO;
using System.Linq;

using Mooh.Presentation.Music;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the shipped instruments' key-off and fade timing.</summary>
    public sealed class MusicReleaseTests
    {
        private const int Rate = 44100;
        private const int TickFrames = (int)(Rate / (145 * 0.4));
        private const int ReleaseFrame = TickFrames * 3;

        /// <summary>Key-off stops an instrument without a volume envelope regardless of fadeout.</summary>
        [Fact]
        public void NoEnvelopeKeyOffStopsInsteadOfStartingAnInstrumentFade()
        {
            SkaleModule module = Instrument(11, true);
            Assert.False(module.Instruments[11].Envelope(1)!.Enabled);
            Assert.Equal(1024, module.Instruments[11].Fadeout);
            float[] output = Render(module, ReleaseFrame + TickFrames);
            Assert.Contains(output.Take(ReleaseFrame * 2), value => value > 0);
            Assert.Equal(0, output[(ReleaseFrame + 300) * 2]);
        }

        /// <summary>The first release tick retains full fade and each following tick applies the prior decrement.</summary>
        [Fact]
        public void EnvelopeReleaseUsesTheCurrentFadeBeforeDecrementingIt()
        {
            SkaleModule module = Instrument(7, true);
            SkaleModule reference = Instrument(7, false);
            Assert.Equal(4095, module.Instruments[7].Fadeout);
            float[] output = Render(module, ReleaseFrame + (9 * TickFrames));
            float[] baseline = Render(reference, ReleaseFrame + (9 * TickFrames));
            int first = (ReleaseFrame + 200) * 2;
            Assert.Equal(baseline[first], output[first]);
            int second = (ReleaseFrame + TickFrames + 200) * 2;
            Assert.InRange((double)output[second] / baseline[second], 0.874, 0.876);
            Assert.Equal(0, output[(ReleaseFrame + (8 * TickFrames) + 300) * 2]);
        }

        private static float[] Render(SkaleModule module, int frames)
        {
            float[] output = new float[frames * 2];
            new SkalePlayer(module, Rate, 0).Render(output);
            return output;
        }

        private static SkaleModule Instrument(int index, bool release)
        {
            SkaleModule module = SkaleModule.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "music", "TrackMenu.skm")));
            foreach (SkalePattern pattern in module.Patterns.Values)
            {
                foreach (SkaleCell[] cells in pattern.Channels.Values)
                {
                    Array.Fill(cells, SkaleCell.Empty);
                }
            }

            SkaleInstrument instrument = module.Instruments[index];
            foreach (SkaleSample sample in instrument.Samples.Values)
            {
                sample.Pcm = [.. Enumerable.Repeat(1f, sample.Length)];
            }
            if (instrument.Envelope(1) is { Enabled: true } envelope)
            {
                envelope.Points.Clear();
                envelope.Points.Add((0, 64));
                envelope.Points.Add((100, 64));
            }

            SkaleCell[] channel = module.Patterns[module.Order[0]].Channels[0];
            channel[0] = new SkaleCell(48, index + 1, 128, 0, 0);
            if (release)
            {
                channel[1] = new SkaleCell(129, 0, 129, 0, 0);
            }
            return module;
        }
    }
}
