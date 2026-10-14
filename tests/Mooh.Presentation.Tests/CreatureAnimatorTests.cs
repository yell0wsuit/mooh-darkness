using System.Linq;

using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the small beetles and the bio mass show their recovered <c>CGameObjSmBeetle</c> and
    /// <c>CGameObjBioMass</c> states.
    /// </summary>
    public sealed class CreatureAnimatorTests
    {
        /// <summary>Verifies a beetle released by a killed monster flies on its color's page.</summary>
        [Fact]
        public void AReleasedBeetleFliesOnItsColorsPage()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("MGrL", 5, 1), ("WBomb", 4, 1)));
            harness.Step(0.016);
            harness.Detonate(harness.Level.GetEntity(201));

            harness.StepUntil(() => harness.Level.World.Entities.Any(entity => entity.SmallBeetleBody is not null));
            harness.Step(0.016);

            Entity beetle = harness.Level.World.Entities.Single(entity => entity.SmallBeetleBody is not null);
            EntityAnimator animator = harness.Animator(beetle);
            Assert.StartsWith("st_fly", animator.Animation.State);
            Assert.Equal("SpriteObjSmBeetle", animator.Page);
            Assert.NotNull(animator.Frame);
        }

        /// <summary>Verifies the bio mass idles in step with the simulation's own loop.</summary>
        [Fact]
        public void TheBioMassIdlesInStepWithTheSimulation()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("MMBio", 5, 1)));

            harness.Step(0.016);

            Entity bio = harness.Level.GetEntity(200);
            BioMassBody body = bio.BioMassBody!;
            EntityAnimator animator = harness.Animator(bio);
            Assert.Equal("st_stay", animator.Animation.State);
            Assert.Equal(20 - (body.RemainingSeconds / body.StayFrameSeconds), animator.Animation.Position, 6);
        }

        /// <summary>Verifies a spawning bio mass plays a spawn state.</summary>
        [Fact]
        public void ASpawningBioMassPlaysASpawnState()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("MMBio", 5, 1)));
            Entity bio = harness.Level.GetEntity(200);

            harness.StepUntil(() => bio.BioMassBody!.State == BioMassState.Spawn, limitSeconds: 60);

            Assert.Equal("st_spawnhor", harness.Animator(bio).Animation.State);
        }
    }
}
