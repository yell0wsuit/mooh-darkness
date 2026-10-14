using System;
using System.IO;

using Mooh.Presentation.Audio;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the recovered <c>Sound.ini</c> catalog and the original's sound placement.</summary>
    public sealed class SoundTests
    {
        private static readonly Lazy<SoundCatalog> Catalog = new(() =>
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "sounds", "Sound.ini"));
            return SoundCatalog.Load(stream);
        });

        /// <summary>Verifies an alias lists its files and buffer count, at full volume and pitch.</summary>
        [Fact]
        public void AnAliasListsItsFiles()
        {
            SoundDefinition heart = Catalog.Value["HeartWrn1"];

            Assert.Equal(["PlHeartWrn1_01", "PlHeartWrn1_02", "PlHeartWrn1_03", "PlHeartWrn1_04"], heart.Files);
            Assert.Equal(2, heart.Buffers);
            Assert.Equal((1.0, 1.0), heart.Volume);
            Assert.Equal((1.0, 1.0), heart.Frequency);
        }

        /// <summary>Verifies the volume and frequency ranges, single values and silent aliases.</summary>
        [Fact]
        public void RangesAndSilentAliasesAreRead()
        {
            Assert.Equal((1.4, 1.4), Catalog.Value["PlayerFinal"].Volume);
            Assert.Equal((1.0, 1.0), Catalog.Value["PlayerFinal"].Frequency);
            Assert.Empty(Catalog.Value["AmbientWhats"].Files);
            Assert.False(Catalog.Value.Contains("_PlVoice1_01"));
        }

        /// <summary>
        /// Verifies <c>ObjSnd</c>: full volume within 300 px of the screen center, falling to 0.2 at
        /// 900 px, and panned by <c>CNT_SndLFunc</c> and <c>CNT_SndRFunc</c>, which cross at 0.6 in
        /// the middle.
        /// </summary>
        [Fact]
        public void PlacementFallsOffAndPans()
        {
            SoundPlacement center = SoundPlacement.At(400, 300);
            Assert.Equal((0.6, 0.6, 1.0), (Math.Round(center.Left, 9), Math.Round(center.Right, 9), center.Distance));
            Assert.Equal(0.2, SoundPlacement.At(400 + 900, 300).Distance, 9);
            Assert.Equal(0.6, SoundPlacement.At(400 + 600, 300).Distance, 9);

            // 400 px out: 1 - (100 / 600 * 0.8) of the volume; fully left, a fifth right.
            SoundPlacement left = SoundPlacement.At(0, 300);
            Assert.Equal(1 - (0.8 / 6), left.Distance, 9);
            Assert.Equal(left.Distance, left.Left, 9);
            Assert.Equal(0.2 * left.Distance, left.Right, 9);
        }
    }
}
