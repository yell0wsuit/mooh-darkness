using System;
using System.IO;
using System.Linq;

using Mooh.Presentation.Music;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks sample traversal and stop ramps against the original mixer arithmetic.</summary>
    public sealed class MusicMixerTests
    {
        /// <summary>Pitch uses integer Hertz, eighteen phase bits, and a ten-bit interpolation table.</summary>
        [Theory]
        [InlineData(48, 44100)]
        [InlineData(49, 44100)]
        [InlineData(48, 48000)]
        [InlineData(49, 11025)]
        public void SamplePositionsUseTheNativeFixedPointPrecision(int note, int rate)
        {
            SkaleModule module = Isolated(0, note);
            SkaleModule baseline = Isolated(0, note);
            SkaleSample sample = module.Instruments[0].Samples[0];
            Assert.Equal(SkaleSample.NoLoop, sample.LoopType);
            sample.Pcm = [.. Enumerable.Range(0, sample.Length).Select(i => (float)i / sample.Length)];
            baseline.Instruments[0].Samples[0].Pcm = [.. Enumerable.Repeat(1f, sample.Length)];
            int frequency = NativeFrequency(module.Instruments[0], sample, note);
            long step = ((long)frequency << 18) / rate;
            float[] output = Render(module, rate, 2222);
            float[] reference = Render(baseline, rate, 2222);
            foreach (int frame in new[] { 17, 129, 701, 2221 })
            {
                long phase = step * frame;
                double expected = (phase >> 18) + (((phase & 0x3FFFF) >> 8) / 1024.0);
                double actual = (double)output[frame * 2] / reference[frame * 2] * sample.Length;
                Assert.InRange(actual, expected - 0.0002, expected + 0.0002);
            }
        }

        /// <summary>A key-off without an envelope ramps the current sample gain down over exactly 256 frames.</summary>
        [Fact]
        public void KeyOffRendersTheNativeStopRamp()
        {
            SkaleModule module = Isolated(11, 48);
            SkaleSample sample = module.Instruments[11].Samples[0];
            sample.Pcm = [.. Enumerable.Repeat(1f, sample.Length)];
            module.Patterns[module.Order[0]].Channels[0][1] = new SkaleCell(129, 0, 129, 0, 0);
            const int Rate = 44100;
            const int Release = (int)(Rate / (145 * 0.4)) * 3;
            float[] output = Render(module, Rate, Release + 300);
            double gain = output[(Release - 1) * 2];
            Assert.True(gain > 0);
            foreach (int frame in new[] { 0, 31, 127, 254, 255, 256 })
            {
                double expected = gain * Math.Max(0, 255 - frame) / 256.0;
                Assert.InRange(output[(Release + frame) * 2], expected - 0.000001, expected + 0.000001);
            }
        }

        /// <summary>Mono endings retain the last playable frame and repeat the penultimate sample during the stop ramp.</summary>
        [Fact]
        public void NaturalEndingUsesTheNativeMonoGuardsAndStopRamp()
        {
            SkaleModule module = Isolated(0, 48);
            SkaleModule baseline = Isolated(0, 48);
            SkaleSample sample = module.Instruments[0].Samples[0];
            sample.Pcm = [1, 2, 3, 99];
            baseline.Instruments[0].Samples[0].Pcm = [.. Enumerable.Repeat(1f, 512)];
            int rate = NativeFrequency(module.Instruments[0], sample, 48);
            float[] output = Render(module, rate, 300);
            float[] reference = Render(baseline, rate, 300);
            Assert.Equal(reference[3 * 2] * 3, output[3 * 2], 6);
            double lastGain = reference[3 * 2];
            foreach (int frame in new[] { 0, 31, 127, 254, 255, 256 })
            {
                double expected = 3 * lastGain * Math.Max(0, 255 - frame) / 256.0;
                Assert.InRange(output[(4 + frame) * 2], expected - 0.000001, expected + 0.000001);
            }
        }

        /// <summary>The song's master gain multiplies the mixed signal before host playback volume.</summary>
        [Fact]
        public void ModuleGainScalesTheMixedSignal()
        {
            SkaleModule module = Isolated(0, 48);
            SkaleInstrument instrument = module.Instruments[0];
            SkaleSample sample = instrument.Samples[0];
            sample.Pcm = [.. Enumerable.Repeat(0.25f, sample.Length)];
            float[] output = Render(module, 44100, 300);
            double expected = 0.25 * Math.Min(255 - sample.Panning, 128) * 0.003 * 2 * (1 - instrument.Panning) * instrument.Volume * module.MasterGain;
            Assert.InRange(output[200 * 2], expected - 0.000001, expected + 0.000001);
        }

        /// <summary>The original mix saturates below full scale before the host applies its gain.</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(-1)]
        public void ModuleOutputUsesTheNativeClipLimit(int sign)
        {
            SkaleModule module = Isolated(0, 48);
            SkaleSample sample = module.Instruments[0].Samples[0];
            sample.Pcm = [.. Enumerable.Repeat(100f * sign, sample.Length)];
            float[] output = Render(module, 44100, 300);
            Assert.Equal(sign * (32764f / 32768), output[200 * 2]);
        }

        private static int NativeFrequency(SkaleInstrument instrument, SkaleSample sample, int note)
        {
            int final = instrument.FixedNote[note] ? sample.RelativeNote + instrument.Transpose + 60 : note + instrument.Transpose + sample.RelativeNote;
            double period = 7680 - (64 * final) - ((sample.Finetune + instrument.Finetune) / 2);
            // The original binary stores the reciprocal as a single-precision constant.
            return (int)(8363 * Math.Pow(2, (4608 - period) * (double)(1f / 768)));
        }

        private static float[] Render(SkaleModule module, int rate, int frames)
        {
            float[] output = new float[frames * 2];
            new SkalePlayer(module, rate, 0).Render(output);
            return output;
        }

        private static SkaleModule Isolated(int index, int note)
        {
            SkaleModule module = SkaleModule.Load(Path.Combine(AppContext.BaseDirectory, "assets", "music", "TrackMenu.json"));
            foreach (SkalePattern pattern in module.Patterns.Values)
            {
                foreach (SkaleCell[] cells in pattern.Channels.Values)
                {
                    Array.Fill(cells, SkaleCell.Empty);
                }
            }
            module.Patterns[module.Order[0]].Channels[0][0] = new SkaleCell(note, index + 1, 128, 0, 0);
            return module;
        }
    }
}
