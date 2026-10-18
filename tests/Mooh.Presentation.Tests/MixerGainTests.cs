using System;
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
        public void ShippedMixerGainIsPreservedInJson()
        {
            SkaleModule module = SkaleModule.Load(Path.Combine(AppContext.BaseDirectory, "assets", "music", "TrackMenu.json"));
            Assert.Equal(1.3007843494415283, module.MasterGain);
        }

    }
}
