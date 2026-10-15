using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Player;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the simulation's events cue the sounds the original's handlers play.</summary>
    public sealed class EventSoundTests
    {
        /// <summary>Verifies a landing plays the type's <c>SndLand</c>, here a rock's.</summary>
        [Fact]
        public void ALandingPlaysTheTypesLandSound()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ORock01", 4, 0)));

            Assert.Contains("RockLand", Heard(harness, 2));
        }

        /// <summary>
        /// Verifies a struck rock stopping against a wall plays its <c>SndLand</c>, as
        /// <c>PlPush_OnCell</c> does when the chain stops.
        /// </summary>
        [Fact]
        public void AStruckRockStoppingPlaysItsLandSound()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ORock01", 3, 1), ("CellsMetal01", 6, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            harness.StepUntil(() => harness.Player.Position.X == 2);
            harness.Input(default);
            harness.StepUntil(() => harness.Player.Motion is null);
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: false, Down: false, Fire: true));
            bool landed = false;

            harness.StepUntil(() => landed |= harness.LastEvents.OfType<Core.Simulation.StrikePushStopped>().Any()
                && harness.Presenter.Sounds.Any(cue => cue.Alias == "RockLand"));

            Assert.True(landed);
        }

        /// <summary>Verifies eating plays the type's <c>SndEat</c>, here a coin's.</summary>
        [Fact]
        public void EatingPlaysTheTypesEatSound()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OCoin01", 2, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            Assert.Contains("CoinEat", Heard(harness, 1));
        }

        /// <summary>Verifies a blast plays its source's <c>SndExpl</c>, here a grenade's.</summary>
        [Fact]
        public void ABlastPlaysItsSourcesSound()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 5, 1)));
            harness.Step(0.016);
            harness.Detonate(harness.Level.GetEntity(200));

            Assert.Contains("GrenadeExpl", Heard(harness, 0.1));
        }

        /// <summary>
        /// Verifies a hurt player plays <c>OnHelthLost</c>'s three sounds: the heal or fatal cue, the
        /// voice and the strike.
        /// </summary>
        [Fact]
        public void AHurtPlayerPlaysItsThreeSounds()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Step(0.016);
            harness.Damage(10);

            List<string> heard = Heard(harness, 0.05);

            Assert.Contains("PlayerHeal", heard);
            Assert.Contains("PlayerVoice", heard);
            Assert.Contains("PlayerStrike", heard);
        }

        private static List<string> Heard(SceneHarness harness, double seconds)
        {
            List<string> heard = [];
            for (double elapsed = 0; elapsed < seconds; elapsed += 0.016)
            {
                harness.Step(0.016);
                heard.AddRange(harness.Presenter.Sounds.Select(cue => cue.Alias));
            }

            return heard;
        }
    }
}
