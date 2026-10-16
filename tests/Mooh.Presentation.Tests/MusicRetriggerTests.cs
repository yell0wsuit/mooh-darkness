using System;
using System.IO;
using System.Linq;

using Mooh.Presentation.Music;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks detached sample playback when a channel starts its next note.</summary>
    public sealed class MusicRetriggerTests
    {
        private const int Rate = 44100;
        private const int TickFrames = (int)(Rate / (145 * 0.4));
        private const int RetriggerFrame = TickFrames * 3;

        /// <summary>An ordinary retrigger preserves the preceding sample through its 256-frame stop ramp.</summary>
        [Fact]
        public void OrdinaryRetriggerKeepsThePreviousVoiceStopRamp()
        {
            SkaleModule module = Instrument(11, true);
            float[] output = Render(module, RetriggerFrame + 300);
            double gain = output[(RetriggerFrame - 1) * 2];
            Assert.True(gain > 0);
            foreach (int frame in new[] { 0, 31, 127, 254, 255, 256 })
            {
                double expected = gain * Math.Max(0, 255 - frame) / 256.0;
                Assert.InRange(output[(RetriggerFrame + frame) * 2], expected - 0.000001, expected + 0.000001);
            }
        }

        /// <summary>The string instrument releases its old envelope independently of the silent replacement note.</summary>
        [Fact]
        public void StringRetriggerPreservesItsEnvelopeRelease()
        {
            SkaleModule module = Instrument(8, true);
            SkaleModule reference = Instrument(8, false);
            float[] output = Render(module, RetriggerFrame + (7 * TickFrames));
            float[] baseline = Render(reference, output.Length / 2);
            int first = (RetriggerFrame + 300) * 2;
            Assert.Equal(baseline[first], output[first], 6);
            int later = (RetriggerFrame + (5 * TickFrames) + 200) * 2;
            int volume = (65536 - (5 * 400)) * 128 / 65536;
            Assert.InRange((double)output[later] / baseline[later], (volume / 128.0) - 0.000001, (volume / 128.0) + 0.000001);
        }

        /// <summary>Host buffer sizes do not change playback through repeated notes and detached tails.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(56)]
        public void RepeatedNotesRenderIdenticallyAcrossBufferBoundaries(int order)
        {
            SkaleModule module = SkaleModule.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "music", "TrackMenu.skm")));
            foreach (SkaleSample sample in module.Instruments.Values.SelectMany(instrument => instrument.Samples.Values))
            {
                sample.Pcm = [.. Enumerable.Range(0, sample.Length).Select(i => (float)Math.Sin(i * 0.05))];
            }
            float[] continuous = new float[Rate * 5 * 2];
            float[] chunked = new float[continuous.Length];
            new SkalePlayer(module, Rate, order).Render(continuous);
            SkalePlayer player = new(module, Rate, order);
            for (int at = 0; at < chunked.Length; at += 514)
            {
                player.Render(chunked.AsSpan(at, Math.Min(514, chunked.Length - at)));
            }
            Assert.Contains(continuous, value => Math.Abs(value) > 0.01);
            Assert.Equal(continuous, chunked);
        }

        private static float[] Render(SkaleModule module, int frames)
        {
            float[] output = new float[frames * 2];
            new SkalePlayer(module, Rate, 0).Render(output);
            return output;
        }

        private static SkaleModule Instrument(int index, bool retrigger)
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
                envelope.Points.Add((10000, 64));
            }
            SkaleCell[] channel = module.Patterns[module.Order[0]].Channels[0];
            channel[0] = new SkaleCell(48, index + 1, 128, 0, 0);
            if (retrigger)
            {
                channel[1] = new SkaleCell(48, index + 1, 0, 0, 0);
            }

            return module;
        }
    }
}
