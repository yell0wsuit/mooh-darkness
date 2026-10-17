using System;
using System.Linq;

using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Hud;
using Mooh.Presentation.Text;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the screen panels of <c>PnlInit</c>, <c>WeapInit</c> and <c>BonInit</c>, and the
    /// help panel of <c>HlpAdd</c>, <c>HlpCycle</c> and <c>HlpRender</c>.
    /// </summary>
    public sealed class HudPresenterTests
    {
        /// <summary>The centered key group stays together when the side panels move outward.</summary>
        [Fact]
        public void WidescreenKeepsAllThreeKeysCentered()
        {
            HudRun run = new(SceneHarness.BuildLevel(1, ("OKDKey1", 2, 1), ("OKDKey2", 3, 1), ("OKDKey3", 4, 1)))
            {
                Camera = new Scene.SceneCamera { HorizontalMargin = 400.0 / 3 }
            };
            run.Step(0.016);
            Assert.Equal(new HudSprite("PSmKeyBlue", 480, 600), run.Sprite("PSmKeyBlue"));
            Assert.Equal(new HudSprite("PSmKeyRed", 320, 600), run.Sprite("PSmKeyRed"));
            Assert.Equal(new HudSprite("PSmKeyYellow", 400, 600), run.Sprite("PSmKeyYellow"));
        }

        /// <summary>
        /// Verifies a level without monsters, keys, bonuses or other weapons shows only the health,
        /// score and grenade panels, each with its value beside it.
        /// </summary>
        [Fact]
        public void APlainLevelShowsHealthScoreAndGrenades()
        {
            HudRun run = new(SceneHarness.BuildLevel(1));

            run.Step(0.016);

            Assert.Equal(["PNrmHealth", "PNrmScore", "PSelWeapGr"], run.Hud.Sprites.Select(sprite => sprite.Frame).Order());
            Assert.Equal(new HudSprite("PNrmHealth", 36, 51), run.Sprite("PNrmHealth"));
            Assert.Equal(new HudText("100", 56, 81, TextAlignment.Left), run.Hud.Texts.Single(text => text.Y == 81));
            Assert.Equal(new HudText("0", 56, 151, TextAlignment.Left), run.Hud.Texts.Single(text => text.Y == 151));
            Assert.Equal(new HudText("5", 56, 583, TextAlignment.Left), run.Hud.Texts.Single(text => text.Y == 583));
        }

        /// <summary>
        /// Verifies a level holding monsters shows their panels on the right with the live counts,
        /// right-aligned 20 px inside.
        /// </summary>
        [Fact]
        public void MonsterPanelsCountTheMonsters()
        {
            HudRun run = new(SceneHarness.BuildLevel(1, ("MGrL", 5, 1), ("MGrL", 6, 1), ("MBlueL", 7, 1)));

            run.Step(0.016);

            Assert.Equal(new HudSprite("PNrmMonGreen", 765, 121), run.Sprite("PNrmMonGreen"));
            Assert.Equal(new HudSprite("PNrmMonBlue", 765, 51), run.Sprite("PNrmMonBlue"));
            Assert.Contains(new HudText("2", 745, 151, TextAlignment.Right), run.Hud.Texts);
            Assert.Contains(new HudText("1", 745, 81, TextAlignment.Right), run.Hud.Texts);
        }

        /// <summary>
        /// Verifies a key's panel is shown dim while the key lies in the level, and lit once
        /// eaten: the first key lights the blue panel, as <c>KeyDoorKey1</c> does.
        /// </summary>
        [Fact]
        public void AKeysPanelLightsWhenTheKeyIsEaten()
        {
            HudRun run = new(SceneHarness.BuildLevel(1, ("OKDKey1", 2, 1)));
            run.Step(0.016);
            Assert.Equal(new HudSprite("PSmKeyBlue", 480, 600), run.Sprite("PSmKeyBlue"));

            run.Harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            run.StepUntil(() => run.Harness.Player.PlayerBody!.Progress.HasKey(1));
            run.Step(0.016);

            Assert.Equal(new HudSprite("PNrmKeyBlue", 480, 600), run.Sprite("PNrmKeyBlue"));
        }

        /// <summary>
        /// Verifies a weapon panel shows only for a level that holds its pickups, dim while its
        /// ammunition is spent, and the selected weapon uses its highlighted frame.
        /// </summary>
        [Fact]
        public void AWeaponPanelShowsItsAmmunition()
        {
            HudRun run = new(SceneHarness.BuildLevel(1, ("WRBox", 5, 1)));

            run.Step(0.016);

            Assert.Equal(new HudSprite("PSmWeapRock", 36, 493), run.Sprite("PSmWeapRock"));
            Assert.DoesNotContain(run.Hud.Texts, text => text.Y == 523);
            Assert.Equal(new HudSprite("PSelWeapGr", 36, 553), run.Sprite("PSelWeapGr"));
        }

        /// <summary>
        /// Verifies a timed level shows the clock panel with the whole seconds left, rounded down.
        /// </summary>
        [Fact]
        public void ATimedLevelShowsTheClock()
        {
            HudRun run = new(SceneHarness.BuildLevel(1), DifficultySpeeds.Timed);

            run.Step(0.5);

            Assert.Equal(new HudSprite("PNrmTime", 36, 51), run.Sprite("PNrmTime"));
            Assert.Contains(new HudText("399", 56, 81, TextAlignment.Left), run.Hud.Texts);
        }

        /// <summary>
        /// Verifies a score gain jerks the score panel up to 6 px off its place, and it springs
        /// back within a second while its value stays put.
        /// </summary>
        [Fact]
        public void AScoreGainJerksTheScorePanel()
        {
            HudRun run = new(SceneHarness.BuildLevel(1));
            run.Step(0.016);

            run.Score = 100;
            run.Step(0.001);
            HudSprite jerked = run.Sprite("PNrmScore");
            double distance = Math.Sqrt(Math.Pow(jerked.X - 36, 2) + Math.Pow(jerked.Y - 121, 2));
            Assert.InRange(distance, 1, 7);
            Assert.Contains(new HudText("100", 56, 151, TextAlignment.Left), run.Hud.Texts);

            run.Step(1);
            Assert.Equal(new HudSprite("PNrmScore", 36, 121), run.Sprite("PNrmScore"));
        }

        /// <summary>
        /// Verifies a message table's help slides in from above over 250 ms, centered at x 400,
        /// then rests with its first line at y 40 and the next 28 px below.
        /// </summary>
        [Fact]
        public void HelpSlidesInThenRests()
        {
            HudRun run = new(SceneHarness.BuildLevel(1));
            run.Step(0.016);
            run.Step(0.016);
            string[] lines = ["Dig into the earth ", "to move forward."];

            run.Events.Add(new HelpTextRequested(run.Harness.Player.Id, "HlpTut01Dig"));
            run.Step(0.125);
            Assert.Equal(new HudText(lines[0], 400, 40 - ((50 + (2 * 28)) * 125 / 250), TextAlignment.Center), run.Hud.Texts.Single(text => text.Text == lines[0]));

            run.Step(0.2);
            Assert.Equal(new HudText(lines[0], 400, 40, TextAlignment.Center), run.Hud.Texts.Single(text => text.Text == lines[0]));
            Assert.Equal(new HudText(lines[1], 400, 68, TextAlignment.Center), run.Hud.Texts.Single(text => text.Text == lines[1]));
        }

        /// <summary>Verifies the help leaves after its five seconds on screen.</summary>
        [Fact]
        public void HelpLeavesAfterFiveSeconds()
        {
            HudRun run = new(SceneHarness.BuildLevel(1));
            run.Step(0.016);
            run.Step(0.016);
            run.Events.Add(new HelpTextRequested(run.Harness.Player.Id, "HlpTutAllObjsFire"));
            run.Step(0.3);

            for (int step = 0; step < 55; step++)
            {
                run.Step(0.1);
            }

            Assert.DoesNotContain(run.Hud.Texts, text => text.Text == "Fire will burn objects.");
        }
    }
}
