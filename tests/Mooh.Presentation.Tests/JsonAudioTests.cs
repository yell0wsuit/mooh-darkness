using System.IO;
using System.Text;

using Mooh.Presentation.Audio;
using Mooh.Presentation.Music;

using Xunit;

namespace Mooh.Presentation.Tests
{
    public sealed class JsonAudioTests
    {
        [Fact]
        public void SoundJsonPreservesRangesAndSilentAliases()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes("{\"silent\":{\"files\":[],\"buffers\":2,\"volume\":[0.5,1.4],\"frequency\":[0.8,1.2]}}"));
            SoundDefinition sound = SoundCatalog.Load(stream)["silent"];
            Assert.Empty(sound.Files);
            Assert.Equal((0.5, 1.4), sound.Volume);
            Assert.Equal((0.8, 1.2), sound.Frequency);
        }

        [Fact]
        public void MusicJsonPreservesCuePositions()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes("{\"game\":{\"file\":\"TrackMenu\",\"position\":40,\"volume\":0.6}}"));
            Assert.Equal(new MusicTrack("game", "TrackMenu", 40, 0.6), MusicCatalog.Load(stream)["game"]);
        }

        [Fact]
        public void ModuleJsonContainsEditableTrackerData()
        {
            byte[] json = Encoding.UTF8.GetBytes("{\"formatVersion\":1,\"title\":\"test\",\"bpm\":145,\"speed\":3,\"masterGain\":0.75,\"order\":[0],\"songLength\":1,\"channelCount\":1,\"patterns\":[{\"index\":0,\"rows\":1,\"channels\":[{\"index\":0,\"cells\":[{\"note\":48,\"instrument\":0,\"volume\":20,\"effect\":15,\"param\":3}]}]}],\"instruments\":[]}");
            SkaleModule module = SkaleModule.Load(json);
            Assert.Equal(145, module.Bpm);
            Assert.Equal(0.75f, module.MasterGain);
            Assert.Equal(new SkaleCell(48, 0, 20, 15, 3), module.Patterns[0].Channels[0][0]);
        }
    }
}
