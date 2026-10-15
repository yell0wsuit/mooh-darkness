using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Desktop.Platform.Audio;
using Mooh.Presentation.Audio;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Verifies the sound board plays cues as <c>soundCash.ObjSoundPlay</c> does.</summary>
    public sealed class SoundBoardTests
    {
        /// <summary>Verifies an alias plays one of its files with the placement's gains.</summary>
        [Fact]
        public void AnAliasPlaysOneOfItsFiles()
        {
            RecordingOutput output = new();
            SoundBoard board = Create(output);

            board.Play("KeyDoor", new SoundPlacement(0.5, 0.25, 1));

            (string file, float left, float right, float rate) = Assert.Single(output.Plays);
            Assert.StartsWith("KeyDoor", Path.GetFileName(file), StringComparison.Ordinal);
            Assert.Equal((0.5f, 0.25f, 1f), (left, right, rate));
        }

        /// <summary>Verifies a centered cue plays unplaced, at full gain, as <c>SndPlay</c> does.</summary>
        [Fact]
        public void ACenteredCuePlaysUnplaced()
        {
            RecordingOutput output = new();
            SoundBoard board = Create(output);
            Presentation.Scene.SceneCamera camera = new();
            camera.Snap(0, 0);

            board.Play([new SoundCue("KeyDoor", 20, 20, Centered: true)], camera);

            (_, float left, float right, _) = Assert.Single(output.Plays);
            Assert.Equal((1f, 1f), (left, right));
        }

        /// <summary>Verifies an alias never plays more at once than its buffer count.</summary>
        [Fact]
        public void AnAliasKeepsToItsBuffers()
        {
            RecordingOutput output = new();
            SoundBoard board = Create(output);

            for (int index = 0; index < 5; index++)
            {
                board.Play("PlayerFinal", SoundPlacement.Centered);
            }

            _ = Assert.Single(output.Plays);
            output.FinishAll();
            board.Play("PlayerFinal", SoundPlacement.Centered);
            Assert.Equal(2, output.Plays.Count);
        }

        /// <summary>Verifies an unknown or silent alias plays nothing.</summary>
        [Fact]
        public void UnknownAndSilentAliasesPlayNothing()
        {
            RecordingOutput output = new();
            SoundBoard board = Create(output);

            board.Play("NoSuchAlias", SoundPlacement.Centered);
            board.Play("AmbientWhats", SoundPlacement.Centered);

            Assert.Empty(output.Plays);
        }

        private static SoundBoard Create(IAudioOutput output)
        {
            string sounds = Path.Combine(AppContext.BaseDirectory, "assets", "sounds");
            using FileStream stream = File.OpenRead(Path.Combine(sounds, "Sound.ini"));
            return new SoundBoard(SoundCatalog.Load(stream), output, sounds, new Random(1));
        }

        private sealed class RecordingOutput : IAudioOutput
        {
            private readonly List<Voice> _voices = [];

            public List<(string File, float Left, float Right, float Rate)> Plays { get; } = [];

            public object? Load(string path)
            {
                return File.Exists(path) ? path : null;
            }

            public IVoice Play(object sound, float left, float right, float rate)
            {
                Plays.Add(((string)sound, left, right, rate));
                Voice voice = new();
                _voices.Add(voice);
                return voice;
            }

            public void FinishAll()
            {
                _voices.ForEach(voice => voice.IsPlaying = false);
            }

            public void Dispose()
            {
            }

            public (float[] Samples, int Rate)? DecodeMono(byte[] encoded)
            {
                return null;
            }

            public IAudioFeed? OpenFeed(int rate)
            {
                return null;
            }

            private sealed class Voice : IVoice
            {
                public bool IsPlaying { get; set; } = true;
            }
        }
    }
}
