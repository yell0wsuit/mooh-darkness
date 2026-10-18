using System;

using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the placed big bomb's recovered animation-driven tick sounds.</summary>
    public sealed class BigBombSoundTests
    {
        /// <summary>After its initial animation, thirty ticks alternate every 225 milliseconds.</summary>
        [Fact]
        public void PlacedBigBombTicksThirtyTimesAfterItsInitialAnimation()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Level.World.Add(new Entity(EntityId.New(), new GridPosition(5, 1),
                InteractionMask.BlocksMovement, bigBombBody: new BigBombBody()));
            harness.Presenter.Update(harness.Level.World, [], 0);
            harness.Presenter.Update(harness.Level.World, [], 0.700);
            Assert.DoesNotContain(harness.Presenter.Sounds, cue => cue.Alias.StartsWith("BBombTick", StringComparison.Ordinal));

            for (int tick = 0; tick < 30; tick++)
            {
                harness.Presenter.Update(harness.Level.World, [], 0.225);
                string expected = tick % 2 == 0 ? "BBombTick1" : "BBombTick2";
                Assert.Equal(expected, Assert.Single(harness.Presenter.Sounds,
                    cue => cue.Alias.StartsWith("BBombTick", StringComparison.Ordinal)).Alias);
            }

            harness.Presenter.Update(harness.Level.World, [], 0.225);
            Assert.DoesNotContain(harness.Presenter.Sounds, cue => cue.Alias.StartsWith("BBomb", StringComparison.Ordinal));
        }
    }
}
