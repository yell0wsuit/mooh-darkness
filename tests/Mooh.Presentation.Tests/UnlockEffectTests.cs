using System;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Scene;
using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Exercises completion effects for interactions that do not report ordinary destruction.</summary>
    public sealed class UnlockEffectTests
    {
        /// <summary>Every key door sparks from its final opening frame, even between draw updates.</summary>
        [Theory]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(3, false)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        public void KeyDoorOpeningEndsInFinalFrameSparks(int key, bool bulk)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ($"OKDKey{key}", 2, 1), ($"OKDDoor{key}", 3, 1)), masks: new FinalDoorMask());
            harness.Input(new PlayerInputState(false, false, true, false));
            if (bulk)
            {
                harness.Step(2);
            }
            else
            {
                harness.StepUntil(() => harness.LastEvents.Any(item => item is KeyDoorOpeningStarted));
                Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Frame.StartsWith("PGetStar", StringComparison.Ordinal));
                harness.Input(default);
                harness.StepUntil(() => harness.LastEvents.Any(item => item is KeyDoorOpened));
            }

            SceneSprite[] sparks = [.. harness.Presenter.Sprites.Where(sprite => sprite.Frame.StartsWith("PGetStar", StringComparison.Ordinal))];
            Assert.Equal(9, sparks.Length);
            Assert.All(sparks, spark =>
            {
                Assert.True(spark.Additive);
                Assert.Equal(1, spark.Pass);
                Assert.InRange(spark.X, 2.8, 3.2);
                Assert.InRange(spark.Y, 0.8, 1.2);
            });
            Assert.DoesNotContain(harness.Level.World.Entities, entity => entity.KeyDoorBody is not null);
            harness.Input(default);
            harness.Step(3);
            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Frame.StartsWith("PGetStar", StringComparison.Ordinal));
        }

        /// <summary>The direct fire effect is visible even while its player survives in the cell.</summary>
        [Fact]
        public void FireContactShowsItsExplosionAndSmoke()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OFire", 2, 1)));
            harness.Input(new PlayerInputState(false, false, true, false));
            harness.StepUntil(() => harness.LastEvents.Any(item => item is FireContactResolved));
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Page == "SpriteExpl");
            Assert.Equal(5, harness.Presenter.Sprites.Count(sprite => sprite.Frame.StartsWith("PSmoke", StringComparison.Ordinal)));
        }

        /// <summary>Magic flashes before its new diamond occupies the emptied cell.</summary>
        [Fact]
        public void MagicDiamondShowsItsExplosionAndSmoke()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            EntityId gem = EntityId.New();
            EntityId diamond = EntityId.New();
            harness.Level.World.Add(new Entity(diamond, new GridPosition(4, 1), InteractionMask.BlocksMovement));
            harness.Presenter.Update(harness.Level.World, [new MagicDiamondSpawned(gem, diamond, new GridPosition(4, 1))], 0);
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Page == "SpriteExpl");
            Assert.Equal(5, harness.Presenter.Sprites.Count(sprite => sprite.Frame.StartsWith("PSmoke", StringComparison.Ordinal)));
        }

        /// <summary>Eating a small green monster has the same collection burst as its spawned beetle.</summary>
        [Fact]
        public void EdibleGreenMonsterShowsItsCollectionBurst()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("MGrL", 2, 1)));
            _ = harness.Level.Simulation.SetGreenMonsterGlasses(true);
            harness.Input(new PlayerInputState(false, false, true, false));
            harness.StepUntil(() => harness.LastEvents.Any(item => item is PlayerAte));
            _ = Assert.Single(harness.Presenter.Sprites, sprite => sprite.Frame.StartsWith("PSmall", StringComparison.Ordinal));
            Assert.Equal(10, harness.Presenter.Sprites.Count(sprite => sprite.Page == "SpriteObjMine" && sprite.Frame.StartsWith("Star", StringComparison.Ordinal)));
        }

        private sealed class FinalDoorMask : ISpriteMask
        {
            public SpriteFrame? FrameOf(string page, string frame)
            {
                return page == "SpriteObjKeyDoor"
                && frame.StartsWith("DSt", StringComparison.Ordinal) && frame.EndsWith("_14", StringComparison.Ordinal)
                ? new SpriteFrame(frame, 0, 0, 20, 20, -10, -10) : null;
            }

            public bool IsSolid(string page, int x, int y)
            {
                return true;
            }
        }
    }
}
