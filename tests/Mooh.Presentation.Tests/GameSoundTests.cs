using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Audio;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the sounds the game module plays outside the objects' own: the outcome
    /// jingle, the bonuses' <c>SndOn</c>, <c>SndOff</c> and <c>SndAmb</c>, and <c>WeapInc</c>'s.
    /// </summary>
    public sealed class GameSoundTests
    {
        /// <summary>
        /// Verifies an announced win plays <c>SndPlFinalOk</c> unplaced, as <c>__GStatePlay</c>'s
        /// <c>SndPlay</c> does right after the line, and the settle plays nothing more.
        /// </summary>
        [Fact]
        public void AnAnnouncedWinPlaysTheJingle()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Step(0.016);

            harness.Presenter.Update(harness.Level.World, [new LevelOutcomeAnnounced(harness.Player.Id, LevelOutcome.Won, TimedOut: false)], 0.016);
            SoundCue jingle = Assert.Single(harness.Presenter.Sounds, cue => cue.Alias == "PlayerFinal");
            Assert.True(jingle.Centered);

            harness.Presenter.Update(harness.Level.World, [new LevelWon(harness.Player.Id)], 0.016);
            Assert.DoesNotContain(harness.Presenter.Sounds, cue => cue.Alias == "PlayerFinal");
        }

        /// <summary>Verifies a weapon pickup plays <c>SndWeapGet</c> as its ammunition rises.</summary>
        [Fact]
        public void AWeaponPickupPlaysTheGetSound()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WGrBox", 2, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            bool heard = false;

            harness.StepUntil(() => heard |= harness.Presenter.Sounds.Any(cue => cue.Alias == "WeaponGet"), limitSeconds: 2);

            Assert.True(heard);
        }

        /// <summary>
        /// Verifies drunk cola plays <c>SndBonColaOn</c> as its protection starts, then
        /// <c>SndBonColaAmb</c> every 300 to 350 ms while it lasts.
        /// </summary>
        [Fact]
        public void ColaPlaysItsOnAndAmbientSounds()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("BCola", 2, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            List<string> heard = [];

            harness.StepUntil(() => harness.Player.PlayerBody!.Progress.ColaRemaining > System.TimeSpan.Zero, limitSeconds: 4);
            heard.AddRange(harness.Presenter.Sounds.Select(cue => cue.Alias));
            for (int frame = 0; frame < 50; frame++)
            {
                harness.Step(0.016);
                heard.AddRange(harness.Presenter.Sounds.Select(cue => cue.Alias));
            }

            Assert.Equal("ColaOn", heard.First(alias => alias.StartsWith("Cola", System.StringComparison.Ordinal) && alias != "ColaDrink" && alias != "ColaPlDrink" && alias != "ColaFizz"));
            Assert.InRange(heard.Count(alias => alias == "ColaAmb"), 2, 3);
        }
    }
}
