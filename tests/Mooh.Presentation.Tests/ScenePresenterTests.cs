using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Player;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies the presenter's draw list follows the simulated world.</summary>
    public sealed class ScenePresenterTests
    {
        /// <summary>A teleporter's saved links, <c>CLinkCoordBase</c>: one link to column 6.</summary>
        private static readonly int[][][] FarLink = [[[6, 1]]];

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

        /// <summary>
        /// Verifies a teleporting player is drawn along the straight line of the flight, over
        /// everything else, as <c>_TPortCFunc</c> and <c>ST_TPortMove</c>'s <c>RPass=2</c> do.
        /// </summary>
        [Fact]
        public void ATeleportingPlayerIsDrawnAlongTheFlightLine()
        {
            LevelDocument level = SceneHarness.BuildLevel(1);
            JsonElement links = JsonSerializer.SerializeToElement(FarLink);
            LevelObject teleporter = new(300, "OTPort", 0, new LevelPosition(1, 1), [new LevelSavedState("GameLinkBase.CLinkCoordBase", links)], new Dictionary<string, JsonElement>());
            SceneHarness harness = new(level with
            {
                Objects = [.. level.Objects!, teleporter]
            });
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: false, Down: false, Fire: true));
            harness.StepUntil(() => harness.Player.PlayerBody!.Action?.Kind == PlayerActionKind.TeleportFlight);
            harness.Input(default);

            harness.StepUntil(() => harness.Player.PlayerBody!.Teleport!.Progress >= 0.5);

            SceneSprite player = harness.Presenter.Sprites.Single(sprite => sprite.Entity == harness.Player.Id);
            double expected = 1 + (5 * harness.Player.PlayerBody!.Teleport!.Progress);
            Assert.Equal(expected, player.X, 6);
            Assert.Equal(1, player.Y);
            Assert.Equal(2, player.Pass);
            Assert.Equal(player, harness.Presenter.Sprites[^1]);
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
