using System;
using System.Buffers.Binary;
using System.IO;

using Mooh.Presentation.Music;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks the module mixer gain stored independently of track playback volume.</summary>
    public sealed class MixerGainTests
    {
        /// <summary>The shipped module specifies a mixer gain above unity.</summary>
        [Fact]
        public void ShippedMixerGainIsReadFromItsVersionTwoChunk()
        {
            SkaleModule module = SkaleModule.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "music", "TrackMenu.skm")));
            Assert.Equal(1.3007843494415283, module.MasterGain);
        }

        /// <summary>Both initialized mixer formats share a leading gain; absent or unknown formats retain unity.</summary>
        [Theory]
        [InlineData(1, 0.75)]
        [InlineData(2, 0.75)]
        [InlineData(3, 1)]
        [InlineData(0, 1)]
        public void MixerVersionsUseTheirLeadingGainOrTheDefault(int version, double expected)
        {
            byte[] data = new byte[version == 0 ? 17 : 29];
            BinaryPrimitives.WriteUInt32LittleEndian(data, 0xFFFE0001);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 9);
            "ALIM3"u8.CopyTo(data.AsSpan(8));
            if (version != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(17), 0x00050000u | (uint)version);
                BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(21), 4);
                BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(25), 0.75f);
            }

            Assert.Equal(expected, SkaleModule.Load(data).MasterGain);
        }
    }
}
