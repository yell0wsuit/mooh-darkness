using System;
using System.IO;

using Mooh.Core.Content;
using Mooh.Core.Player;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the floating scores and lines of <c>GameScoreAdd</c>: the score 20 px above the
    /// object, its line 20 px below, drifting off for three seconds.
    /// </summary>
    public sealed class FloatingTextTests
    {
        /// <summary>
        /// Verifies an eaten coin floats its score and, while the treasure bonus is open,
        /// <c>FObjCoin</c>.
        /// </summary>
        [Fact]
        public void AnEatenCoinFloatsItsScoreAndTreasureLine()
        {
            SceneHarness harness = Harness(("OCoin01", 2, 1));

            EatRight(harness);

            SceneText score = Assert.Single(harness.Presenter.Texts, text => text.Text == "1");
            Assert.Equal("PanelDigFnt", score.Font);
            SceneText line = Assert.Single(harness.Presenter.Texts, text => text.Text == "+ treasure");
            Assert.Equal("PanelDigTextFnt", line.Font);
            Assert.True(line.Y > score.Y);
        }

        /// <summary>Verifies an eaten grenade box floats its count, <c>FObjWeapGrenades % 5</c>.</summary>
        [Fact]
        public void AnEatenGrenadeBoxFloatsItsCount()
        {
            SceneHarness harness = Harness(("WGrBox", 2, 1));

            EatRight(harness);

            Assert.Contains(harness.Presenter.Texts, text => text.Text == "+5 grenades");
            Assert.Contains(harness.Presenter.Texts, text => text.Text == "100");
        }

        /// <summary>Verifies a hurt player floats the loss in red, <c>FObjHealLost</c>.</summary>
        [Fact]
        public void AHurtPlayerFloatsTheLossInRed()
        {
            SceneHarness harness = Harness();
            harness.Step(0.016);

            harness.Damage(10);
            harness.Step(0.016);

            SceneText loss = Assert.Single(harness.Presenter.Texts);
            Assert.Equal("-10 health", loss.Text);
            Assert.Equal("PanelDigTextRedFnt", loss.Font);
        }

        /// <summary>Verifies the texts are gone after their three seconds.</summary>
        [Fact]
        public void TextsLastThreeSeconds()
        {
            SceneHarness harness = Harness();
            harness.Step(0.016);
            harness.Damage(10);

            for (int frame = 0; frame < 190; frame++)
            {
                harness.Step(0.016);
            }

            Assert.Empty(harness.Presenter.Texts);
        }

        /// <summary>Verifies this port suppresses floating pickup and score labels by default.</summary>
        [Fact]
        public void PickupsDoNotFloatTextByDefault()
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "text", "strings.bin"));
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OCoin01", 2, 1)), strings: StringTable.Load(stream));
            harness.Input(new PlayerInputState(false, false, true, false));

            harness.Step(0.6);

            Assert.True(harness.Player.PlayerBody!.Progress.Score > 0);
            Assert.Empty(harness.Presenter.Texts);
        }

        /// <summary>Verifies this port suppresses floating damage labels by default.</summary>
        [Fact]
        public void DamageDoesNotFloatTextByDefault()
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "text", "strings.bin"));
            SceneHarness harness = new(SceneHarness.BuildLevel(1), strings: StringTable.Load(stream));
            harness.Step(0.016);

            harness.Damage(10);
            harness.Step(0.016);

            Assert.Equal(90, harness.Player.HealthBody!.Current);
            Assert.Empty(harness.Presenter.Texts);
        }

        private static SceneHarness Harness(params (string Type, int X, int Y)[] extra)
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "text", "strings.bin"));
            return new SceneHarness(SceneHarness.BuildLevel(1, extra), strings: StringTable.Load(stream), showFloatingText: true);
        }

        private static void EatRight(SceneHarness harness)
        {
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            harness.StepUntil(() => harness.Presenter.Texts.Count > 0, limitSeconds: 2);
        }
    }
}
