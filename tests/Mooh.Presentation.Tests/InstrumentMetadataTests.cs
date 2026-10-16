using System;
using System.IO;

using Mooh.Presentation.Music;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks instrument metadata that controls retriggered voices.</summary>
    public sealed class InstrumentMetadataTests
    {
        /// <summary>The strings release their previous voice; other shipped instruments use a short stop ramp.</summary>
        [Fact]
        public void ShippedStringsRetainTheirPreviousVoiceOnANewNote()
        {
            SkaleModule module = SkaleModule.Load(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "assets", "music", "TrackMenu.skm")));
            foreach (SkaleInstrument instrument in module.Instruments.Values)
            {
                Assert.Equal(instrument.Index == 8 ? 1 : 0, instrument.NewNoteAction);
            }
            Assert.Equal(128, module.Instruments[8].Fadeout);
            Assert.True(module.Instruments[8].Envelope(1)!.Enabled);
        }
    }
}
