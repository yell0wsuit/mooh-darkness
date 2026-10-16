using System;
using System.IO;
using System.Linq;

using Mooh.Presentation.Music;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks sample attacks before a ping-pong loop begins.</summary>
    public sealed class MusicLoopTests
    {
        /// <summary>The shipped looping instrument starts at its attack, before loop frame 1992.</summary>
        [Fact]
        public void PingPongSamplePlaysItsOpeningBeforeEnteringTheLoop()
        {
            SkaleModule module = IsolatedInstrument();
            SkaleSample sample = module.Instruments[8].Samples[0];
            Assert.Equal(SkaleSample.PingPongLoop, sample.LoopType);
            Assert.Equal(1992, sample.LoopStart);
            float[] pcm = new float[sample.Length];
            Array.Fill(pcm, 1f, 0, sample.LoopStart);
            sample.Pcm = pcm;
            SkalePlayer player = new(module, 44100, 0);
            float[] output = new float[128];
            player.Render(output);
            Assert.True(output.Any(value => value > 0), "The sample attack must be audible before the silent loop.");
        }

        /// <summary>The first reverse frame starts at the last sample, preserving fractional pitch.</summary>
        [Fact]
        public void PingPongTurnsAtTheLastSampleAndPreservesStateAcrossWrites()
        {
            SkaleModule module = IsolatedInstrument();
            SkaleModule reference = IsolatedInstrument();
            SkaleInstrument instrument = module.Instruments[8];
            SkaleSample sample = instrument.Samples[0];
            sample.Pcm = [.. Enumerable.Range(0, sample.Length).Select(i => (float)i / sample.Length)];
            reference.Instruments[8].Samples[0].Pcm = [.. Enumerable.Repeat(1f, sample.Length)];
            int note = Math.Clamp(60 + instrument.Transpose + sample.RelativeNote, 0, 126);
            int finetune = sample.Finetune + instrument.Finetune;
            double step = 8363 * Math.Pow(2, (4608 - (7680 - (64 * note) - (finetune / 2))) / 768.0) / 44100;
            int turnFrame = (int)Math.Ceiling(sample.LoopEnd / step);
            float[] output = new float[(turnFrame + 2) * 2];
            float[] baseline = new float[output.Length];
            SkalePlayer player = new(module, 44100, 0);
            SkalePlayer oneWrite = new(module, 44100, 0);
            float[] continuous = new float[output.Length];
            oneWrite.Render(continuous);
            for (int at = 0; at < output.Length; at += 254)
            {
                player.Render(output.AsSpan(at, Math.Min(254, output.Length - at)));
            }
            Assert.Equal(continuous, output);
            new SkalePlayer(reference, 44100, 0).Render(baseline);
            double phase = turnFrame * step;
            double expected = sample.LoopEnd - 1 - (phase - Math.Floor(phase));
            double actual = (double)output[turnFrame * 2] / baseline[turnFrame * 2] * sample.Length;
            Assert.InRange(actual, expected - 0.003, expected + 0.003);
        }

        private static SkaleModule IsolatedInstrument()
        {
            SkaleModule module = SkaleModule.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "music", "TrackMenu.skm")));
            foreach (SkalePattern pattern in module.Patterns.Values)
            {
                foreach (SkaleCell[] cells in pattern.Channels.Values)
                {
                    Array.Fill(cells, SkaleCell.Empty);
                }
            }

            module.Patterns[module.Order[0]].Channels[0][0] = new SkaleCell(60, 9, 128, 0, 0);
            return module;
        }
    }
}
