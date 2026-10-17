using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Presentation.Music;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Checks module loading and a regression snapshot of the Skale replay engine.
    /// </summary>
    public sealed class SkalePlayerTests
    {
        private static string MusicPath(string name)
        {
            return Path.Combine(AppContext.BaseDirectory, "assets", "music", name);
        }

        /// <summary>Verifies the module's header, order list, patterns and instruments.</summary>
        [Fact]
        public void TheModuleReadsItsLayout()
        {
            SkaleModule module = SkaleModule.Load(MusicPath("TrackMenu.json"));

            Assert.Equal("moorhuhn", module.Title);
            Assert.Equal((145, 3), (module.Bpm, module.Speed));
            Assert.Equal(65, module.SongLength);
            Assert.Equal(49, module.Patterns.Count);
            Assert.Equal(14, module.Instruments.Count);
            Assert.Equal(10, module.ChannelCount);
            Assert.Equal(new SkaleCell(48, 1, 0x14, 0x0F, 3), module.Patterns[module.Order[0]].Channels[0][0]);
            // Two of the seventeen samples carry no payload and stay silent, as in the Python port.
            SkaleSample[] samples = [.. module.Instruments.Values.SelectMany(instrument => instrument.Samples.Values)];
            Assert.Equal((17, 15), (samples.Length, samples.Count(sample => sample.Ogg.Length > 0)));
            Assert.Equal(15563, module.Instruments[0].Samples[0].Length);
        }

        /// <summary>Verifies decoded samples are fitted to the length their payload declares.</summary>
        [Fact]
        public void DecodedSamplesFitTheirDeclaredLength()
        {
            SkaleModule module = SkaleModule.Load(MusicPath("TrackMenu.json"));
            SkaleSample sample = module.Instruments[0].Samples[0];

            sample.SetDecoded(new float[15563 + 128]);
            Assert.Equal(15563, sample.Pcm!.Length);
            sample.SetDecoded(new float[100]);
            Assert.Equal(15563, sample.Pcm!.Length);
        }

        /// <summary>Verifies the track list: the game's music starts at order 40.</summary>
        [Fact]
        public void TheTrackListNamesWhereEachTrackStarts()
        {
            using FileStream stream = File.OpenRead(MusicPath("Music.json"));
            IReadOnlyDictionary<string, MusicTrack> tracks = MusicCatalog.Load(stream);

            Assert.Equal(new MusicTrack("game", "TrackMenu", 40, 0.6), tracks["game"]);
            Assert.Equal(new MusicTrack("menu", "TrackMenu", 0, 0.6), tracks["menu"]);
            Assert.Equal(16, tracks["gameend"].Position);
        }

        /// <summary>
        /// Records a render snapshot with every sample replaced by the same sine.
        /// Analytical tests separately verify the source-backed playback rules.
        /// </summary>
        [Fact]
        public void ARenderKeepsItsRegressionSnapshot()
        {
            SkaleModule module = SkaleModule.Load(MusicPath("TrackMenu.json"));
            float[] sine = [.. Enumerable.Range(0, 20000).Select(index => (float)Math.Sin(index * 0.05))];
            foreach (SkaleSample sample in module.Instruments.Values.SelectMany(instrument => instrument.Samples.Values))
            {
                sample.Pcm = sine;
            }

            SkalePlayer player = new(module, 44100, 40);
            float[] frames = new float[44100 * 2 * 2];
            player.Render(frames);

            double energy = frames.Sum(value => (double)value * value);
            Assert.Equal(SnapshotEnergy, energy, 6);
        }

        /// <summary>The energy of the first two seconds from order 40 with the sine samples.</summary>
        private const double SnapshotEnergy = 4418.288364;
    }
}
