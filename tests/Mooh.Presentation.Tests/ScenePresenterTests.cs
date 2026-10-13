using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Player;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the presenter's draw list follows the simulated world.</summary>
    public sealed class ScenePresenterTests
    {
        /// <summary>Verifies a moving player is drawn between its cells, on its page.</summary>
        [Fact]
        public void AMovingPlayerIsDrawnBetweenItsCells()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.Step(0.300);

            SceneSprite player = harness.Presenter.Sprites.Single(sprite => sprite.Entity == harness.Player.Id);
            Assert.Equal("SpritePlayer", player.Page);
            Assert.InRange(player.X, 1.01, 1.99);
            Assert.Equal(1, player.Y);
            Assert.StartsWith("OWlkRight", player.Frame);
        }

        /// <summary>Verifies a hidden player is left out of the draw list.</summary>
        [Fact]
        public void AHiddenPlayerIsNotDrawn()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Begin();

            harness.Step(0.016);

            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Entity == harness.Player.Id);
        }

        /// <summary>Verifies an entity's animator goes with it when the world removes it.</summary>
        [Fact]
        public void ARemovedEntityLosesItsAnimator()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("MGrL", 5, 1)));
            harness.Step(0.016);
            Entity monster = harness.Level.GetEntity(200);
            Assert.NotNull(harness.Presenter.AnimatorOf(monster.Id));

            _ = harness.Level.World.Remove(monster.Id);
            harness.Step(0.016);

            Assert.Null(harness.Presenter.AnimatorOf(monster.Id));
            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Entity == monster.Id);
        }
    }
}
