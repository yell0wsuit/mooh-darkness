using System;
using System.IO;
using System.Linq;

using Mooh.Presentation.Screens;
using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks the original instruction screen's progress wipe and waiting state.</summary>
    public sealed class InstructionsPresentationTests
    {
        /// <summary>The completed half uses the recovered Load1 frame names despite its Load0 field name.</summary>
        [Fact]
        public void ProgressWipesBetweenOriginalLoadingFrames()
        {
            InstructionsPresentation presentation = new(key => key);
            presentation.SetProgress(0.5);
            ScreenClip[] clips = [.. presentation.Elements.OfType<ScreenClip>()];
            Assert.Equal(2, clips.Length);
            Assert.Equal((0.0, 0.0, 392.0, 600.0), (clips[0].X, clips[0].Y, clips[0].Width, clips[0].Height));
            Assert.Equal((392.0, 0.0, 408.0, 600.0), (clips[1].X, clips[1].Y, clips[1].Width, clips[1].Height));
            Assert.Equal(["Bk_OLoad10", "Bk_OLoad11"], clips[0].Elements.OfType<ScreenSprite>().Select(sprite => sprite.Frame));
            Assert.Equal(["Bk_OLoad00", "Bk_OLoad01"], clips[1].Elements.OfType<ScreenSprite>().Select(sprite => sprite.Frame));
            ScreenSprite beetle = Assert.Single(presentation.Elements.OfType<ScreenSprite>());
            Assert.Equal((392.0, 575.0), (beetle.X, beetle.Y));
        }

        /// <summary>The walking beetle has sixteen frames at 75 ms and does not manufacture load progress.</summary>
        [Fact]
        public void BeetleAnimatesWithoutChangingLoadingProgress()
        {
            InstructionsPresentation presentation = new(key => key);
            Assert.Equal("BWlk10", Assert.Single(presentation.Elements.OfType<ScreenSprite>()).Frame);
            presentation.Advance(0.075);
            Assert.Equal("BWlk11", Assert.Single(presentation.Elements.OfType<ScreenSprite>()).Frame);
            presentation.Advance(1.125);
            Assert.Equal("BWlk10", Assert.Single(presentation.Elements.OfType<ScreenSprite>()).Frame);
            Assert.Equal(0, presentation.Progress);
            Assert.False(presentation.IsReady);
        }

        /// <summary>Completion is explicit: a full bar can still be loading until the loader finishes.</summary>
        [Fact]
        public void ReadyReplacesLoadingPromptAndHidesBeetle()
        {
            InstructionsPresentation presentation = new(key => key);
            presentation.SetProgress(1);
            Assert.False(presentation.IsReady);
            Assert.Equal("GameInstrLoad", Assert.Single(presentation.Elements.OfType<ScreenText>()).Text);
            presentation.SetProgress(1, isReady: true);
            Assert.True(presentation.IsReady);
            Assert.Empty(presentation.Elements.OfType<ScreenSprite>());
            ScreenText prompt = Assert.Single(presentation.Elements.OfType<ScreenText>());
            Assert.Equal("GameInstrKey", prompt.Text);
            Assert.Equal((400.0, 565.0, "MenuFntEncTxt"), (prompt.X, prompt.Y, prompt.Font));
        }

        /// <summary>Every loading and beetle frame resolves in the original instruction atlas.</summary>
        [Fact]
        public void UsesExistingInstructionAtlasFrames()
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "sprites", "SpriteInstr.seq"));
            SpritePage page = SpritePage.Load("SpriteInstr", stream);
            InstructionsPresentation presentation = new(key => key);
            for (int index = 0; index < 16; index++)
            {
                Assert.All(presentation.Elements.OfType<ScreenClip>().SelectMany(clip => clip.Elements).OfType<ScreenSprite>(),
                    sprite => Assert.True(page.Contains(sprite.Frame), sprite.Frame));
                Assert.All(presentation.Elements.OfType<ScreenSprite>(), sprite => Assert.True(page.Contains(sprite.Frame), sprite.Frame));
                presentation.Advance(0.075);
            }
        }
    }
}
