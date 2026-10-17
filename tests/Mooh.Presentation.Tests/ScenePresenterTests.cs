using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
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

        /// <summary>Settled terrain frames allocate a published sprite snapshot without rebuilding large scratch buffers.</summary>
        [Fact]
        public void LargeStaticSceneReusesWorkingBuffers()
        {
            LevelDocument source = SceneHarness.BuildLevel(1);
            List<LevelObject> objects = [.. source.Objects!];
            for (int i = 0; i < 1000; i++)
            {
                objects.Add(new LevelObject(1000 + i, "CellsMetal01", 0,
                    new LevelPosition(i % 50, 4 + (i / 50)), [], new Dictionary<string, JsonElement>()));
            }

            MaterializedLevel level = LevelMaterializer.Materialize(source with
            {
                Size = new LevelSize(50, 24),
                Objects = objects
            });
            ScenePresenter presenter = new(SceneHarness.Library, new Random(0));
            for (int i = 0; i < 30; i++)
            {
                presenter.Update(level.World, [], 0);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++)
            {
                presenter.Update(level.World, [], 0);
            }
            long bytesPerFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / 20;

            Assert.True(bytesPerFrame < 250_000, $"Static scene allocated {bytesPerFrame:N0} bytes per frame.");
        }

        /// <summary>Published frames survive later updates, including removals and world rotation.</summary>
        [Fact]
        public void PublishedSpritesRemainStableAcrossUpdates()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("CellsEat01", 5, 0)));
            harness.Presenter.Update(harness.Level.World, [], 0);
            IReadOnlyList<SceneSprite> previous = harness.Presenter.Sprites;
            SceneSprite[] expected = [.. previous];
            EntityId removed = harness.Level.GetEntity(200).Id;
            Assert.True(harness.Level.World.Remove(removed));
            harness.Presenter.Update(harness.Level.World, [], 0,
                new WorldSpin(new GridPosition(2, 2), 0.5));

            Assert.Equal(expected, previous);
            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Entity == removed);
        }

        /// <summary>Equal depth keys keep registration order and each object's glow-shadow-body layers.</summary>
        [Fact]
        public void EqualKeysKeepRegistrationAndLayerOrder()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 5, 1), ("WBomb", 5, 1)));
            harness.Presenter.Update(harness.Level.World, [], 0);
            EntityId first = harness.Level.GetEntity(200).Id;
            EntityId second = harness.Level.GetEntity(201).Id;
            SceneSprite[] layers = [.. harness.Presenter.Sprites.Where(sprite => sprite.Entity == first || sprite.Entity == second)];

            Assert.Equal([first, second], layers.Select(sprite => sprite.Entity).Distinct());
            foreach (EntityId id in new[] { first, second })
            {
                SceneSprite[] ownLayers = [.. layers.Where(sprite => sprite.Entity == id)];
                Assert.Equal(3, ownLayers.Length);
                Assert.True(ownLayers[0].Additive);
                Assert.Equal("SpriteParts", ownLayers[1].Page);
                Assert.False(ownLayers[2].Additive);
            }
        }

        /// <summary>Verifies a moving player is drawn between its cells, on its page.</summary>
        [Fact]
        public void AMovingPlayerIsDrawnBetweenItsCells()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.Step(0.300);

            SceneSprite player = harness.Presenter.Sprites.Single(sprite => sprite.Entity == harness.Player.Id && sprite.Page == "SpritePlayer");
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

        /// <summary>
        /// Verifies sprites draw as the native <c>ListRenderSort</c> orders them: by pass, then by
        /// screen <c>x - y</c>, so of two tiles the one further down and left draws first.
        /// </summary>
        [Fact]
        public void SpritesDrawFromBottomLeftToTopRight()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("CellsEat01", 5, 0), ("CellsEat01", 4, 3)));

            harness.Step(0.016);

            List<SceneSprite> ordinary = [.. harness.Presenter.Sprites.Where(sprite => sprite.Pass == 0)];
            double[] keys = [.. ordinary.Select(sprite => Math.Round((sprite.X * 80) - (sprite.Y * 75)))];
            Assert.Equal(keys.Order(), keys);
            EntityId upperRight = harness.Level.GetEntity(200).Id;
            EntityId lowerLeft = harness.Level.GetEntity(201).Id;
            Assert.True(ordinary.FindIndex(sprite => sprite.Entity == lowerLeft) < ordinary.FindIndex(sprite => sprite.Entity == upperRight));
        }

        /// <summary>
        /// Verifies a player standing on a shadow receiver casts its state's shadow, drawn just
        /// before it, as the native object render does.
        /// </summary>
        [Fact]
        public void AStandingPlayerCastsItsShadow()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));

            harness.Step(0.016);

            List<SceneSprite> sprites = [.. harness.Presenter.Sprites];
            int player = sprites.FindIndex(sprite => sprite.Entity == harness.Player.Id && sprite.Page == "SpritePlayer");
            Assert.Equal(new SceneSprite(harness.Player.Id, "SpriteParts", "ShMed01", 1, 1, 0), sprites[player - 1]);
        }

        /// <summary>Verifies a player with nothing beneath it casts no shadow.</summary>
        [Fact]
        public void APlayerInTheAirCastsNoShadow()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(0));

            harness.Step(0.016);

            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Page == "SpriteParts");
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
