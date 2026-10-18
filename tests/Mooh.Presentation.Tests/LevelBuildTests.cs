using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the level's fly-in, <c>__GSTLvlInInit</c> and <c>__GSTLvlIn</c>: the objects in
    /// view fly in from the view's edges, nearest to the player first, before play starts.
    /// </summary>
    public sealed class LevelBuildTests
    {
        /// <summary>Thousands of exterior tiles share the launch window rather than adding a long queue.</summary>
        [Fact]
        public void ExpandedViewKeepsConstructionTimeBounded()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            SceneCamera camera = new()
            {
                HorizontalMargin = 1600,
                VerticalMargin = 1600
            };
            camera.Snap(1, 1);
            LevelBuild build = new(harness.Level.World, SceneHarness.Library, new System.Random(3), camera, scenery);
            build.Advance(8);
            Assert.True(build.IsDone);
        }

        /// <summary>The final scenery landing keeps the same settling spring after construction as authored objects.</summary>
        [Fact]
        public void SceneryKeepsItsLandingSpringAfterConstruction()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            harness.Presenter.Scenery = scenery;
            SceneCamera camera = new();
            camera.Snap(1, 1);
            LevelBuild build = new(harness.Level.World, SceneHarness.Library, new System.Random(3), camera, scenery);
            build.Advance(10);
            harness.Presenter.Update(harness.Level.World, [], 0, build: build, camera: camera);
            Assert.True(build.IsDone);
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Entity == default && sprite.Page == "SpriteCellMetal"
                && (System.Math.Abs(sprite.X - System.Math.Round(sprite.X)) > 0.001
                    || System.Math.Abs(sprite.Y - System.Math.Round(sprite.Y)) > 0.001));

            build.Advance(2);
            harness.Presenter.Update(harness.Level.World, [], 0, build: build, camera: camera);
            Assert.All(scenery.Layout(camera), sprite => Assert.Contains(sprite, harness.Presenter.Sprites));
        }

        /// <summary>Authored objects and scenery across the expanded view participate in the same shuffle.</summary>
        [Theory]
        [InlineData(1600, 0)]
        [InlineData(0, 1600)]
        public void ExpandedViewObjectsAndSceneryFlyIn(double horizontal, double vertical)
        {
            LevelDocument source = SceneHarness.BuildLevel(1, ("CellsEat01", 20, 1), ("CellsEat01", 1, 20));
            SceneHarness harness = new(source with
            {
                Size = new LevelSize(24, 24)
            });
            harness.Presenter.Scenery = new ExteriorScenery(harness.Level.World, SceneHarness.Library, 42);
            SceneCamera camera = new()
            {
                HorizontalMargin = horizontal,
                VerticalMargin = vertical
            };
            camera.Snap(1, 1);
            LevelBuild build = new(harness.Level.World, SceneHarness.Library, new System.Random(3), camera, harness.Presenter.Scenery);

            harness.Presenter.Update(harness.Level.World, [], 0, build: build, camera: camera);
            Assert.Empty(harness.Presenter.Sprites);

            Entity extra = harness.Level.GetEntity(horizontal > 0 ? 200 : 201);
            bool sawExtraFlight = false;
            bool sawSceneryFlight = false;
            for (int frame = 0; frame < 1000 && !build.IsDone; frame++)
            {
                build.Advance(0.016);
                harness.Presenter.Update(harness.Level.World, [], 0, build: build, camera: camera);
                if (!build.IsDone)
                {
                    sawExtraFlight |= harness.Presenter.Sprites.Any(sprite => sprite.Entity == extra.Id
                        && (System.Math.Abs(sprite.X - extra.Position.X) > 0.1 || System.Math.Abs(sprite.Y - extra.Position.Y) > 0.1));
                    sawSceneryFlight |= harness.Presenter.Sprites.Any(sprite => sprite.Entity == default
                        && (System.Math.Abs(sprite.X - System.Math.Round(sprite.X)) > 0.1
                            || System.Math.Abs(sprite.Y - System.Math.Round(sprite.Y)) > 0.1));
                }
            }
            Assert.True(sawExtraFlight);
            Assert.True(sawSceneryFlight);
            Assert.True(build.IsDone);

            build.Finish();
            harness.Presenter.Update(harness.Level.World, [], 0, build: build, camera: camera);
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Entity == harness.Level.GetEntity(200).Id);
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Entity == harness.Level.GetEntity(201).Id);
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Entity == default);
        }

        /// <summary>Growing the viewport during construction schedules new flights without restarting old ones.</summary>
        [Fact]
        public void ResizingAddsFlightsAndSkipPlacesEverything()
        {
            LevelDocument source = SceneHarness.BuildLevel(1, ("CellsEat01", 20, 1));
            SceneHarness harness = new(source with
            {
                Size = new LevelSize(24, 24)
            });
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            harness.Presenter.Scenery = scenery;
            SceneCamera camera = new();
            camera.Snap(1, 1);
            LevelBuild build = new(harness.Level.World, SceneHarness.Library, new System.Random(3), camera, scenery);
            build.Advance(0.8);
            harness.Presenter.Update(harness.Level.World, [], 0, build: build, camera: camera);
            SceneSprite[] before = [.. harness.Presenter.Sprites];
            camera.HorizontalMargin = 1600;
            camera.VerticalMargin = 1600;
            harness.Presenter.Update(harness.Level.World, [], 0, build: build, camera: camera);
            Assert.All(before, sprite => Assert.Contains(sprite, harness.Presenter.Sprites));
            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Entity == harness.Level.GetEntity(200).Id);

            bool sawNewFlight = false;
            for (int frame = 0; frame < 1000 && !build.IsDone; frame++)
            {
                build.Advance(0.016);
                harness.Presenter.Update(harness.Level.World, [], 0, build: build, camera: camera);
                sawNewFlight |= !build.IsDone && harness.Presenter.Sprites.Any(sprite => sprite.Entity == harness.Level.GetEntity(200).Id
                    && (System.Math.Abs(sprite.X - 20) > 0.1 || System.Math.Abs(sprite.Y - 1) > 0.1));
                if (sawNewFlight)
                {
                    break;
                }
            }
            Assert.True(sawNewFlight);
            build.Finish();
            harness.Presenter.Update(harness.Level.World, [], 0, build: build, camera: camera);
            Assert.True(build.IsDone);
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Entity == harness.Level.GetEntity(200).Id);
            Assert.All(scenery.Layout(camera), sprite => Assert.Contains(sprite, harness.Presenter.Sprites));
        }

        /// <summary>Verifies nothing in view, nor the player, shows for the first 750 ms.</summary>
        [Fact]
        public void TheViewStartsEmpty()
        {
            (SceneHarness harness, LevelBuild build) = Start();

            Step(harness, build, 0.7);

            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Entity != default);
            Assert.False(build.IsDone);
        }

        /// <summary>
        /// Verifies the first object launches at 750 ms from the view's edge, and it is one of
        /// the player's nearest.
        /// </summary>
        [Fact]
        public void TheNearestObjectLaunchesFirst()
        {
            (SceneHarness harness, LevelBuild build) = Start();

            Step(harness, build, 0.76);

            SceneSprite first = Assert.Single(harness.Presenter.Sprites, sprite => sprite.Entity != default);
            Entity entity = harness.Level.World.GetEntity(first.Entity);
            Assert.Equal(1, System.Math.Abs(entity.Position.X - 1) + System.Math.Abs(entity.Position.Y - 1));
        }

        /// <summary>
        /// Verifies every object lands in its own cell with <c>SndLvlBld</c>, and then the player
        /// shows and the build is done.
        /// </summary>
        [Fact]
        public void EveryObjectLandsInItsCell()
        {
            (SceneHarness harness, LevelBuild build) = Start();
            List<string> heard = [];

            for (int frame = 0; frame < 400 && !build.IsDone; frame++)
            {
                Step(harness, build, 0.016);
                heard.AddRange(harness.Presenter.Sounds.Select(cue => cue.Alias));
            }

            Assert.True(build.IsDone);
            Assert.Equal(harness.Level.World.Entities.Count - 1, heard.Count(alias => alias == "LevelBuild"));
            Step(harness, build, 1);
            Assert.All(
                harness.Presenter.Sprites.Where(sprite => sprite.Page == "SpriteObjs"),
                sprite => Assert.Equal(
                    harness.Level.World.GetEntity(sprite.Entity).Position.X,
                    sprite.X,
                    3));
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Entity == harness.Player.Id);
        }

        /// <summary>Verifies a key press puts every object in place at once.</summary>
        [Fact]
        public void FinishingPutsEverythingInPlace()
        {
            (SceneHarness harness, LevelBuild build) = Start();
            Step(harness, build, 0.3);

            build.Finish();
            Step(harness, build, 0.016);

            Assert.True(build.IsDone);
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Entity == harness.Player.Id);
        }

        /// <summary>Verifies the last landing still sounds on the frame that finishes the build.</summary>
        [Fact]
        public void TheFinalLandingsSound()
        {
            (SceneHarness harness, LevelBuild build) = Start();

            build.Advance(10);
            harness.Presenter.Update(harness.Level.World, [], 0, build: build);

            Assert.True(build.IsDone);
            Assert.Equal(harness.Level.World.Entities.Count - 1,
                harness.Presenter.Sounds.Count(cue => cue.Alias == "LevelBuild"));
        }

        /// <summary>Verifies pickup glints do not reveal a hidden object before play initialization.</summary>
        [Fact]
        public void PickupsDoNotSparkleBeforePlay()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ODiam01", 2, 1)));
            LevelBuild build = new(harness.Level.World, SceneHarness.Library, new System.Random(3));

            Step(harness, build, 0.016);

            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Pass == int.MaxValue);
        }

        /// <summary>Verifies the start door is created visually after the build, as the player's GameInit does.</summary>
        [Fact]
        public void TheStartDoorDoesNotFlyIn()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ORock01", 2, 1)));
            harness.Begin();
            Entity door = Assert.Single(harness.Level.World.Entities, entity => entity.DoorBody is not null);
            LevelBuild build = new(harness.Level.World, SceneHarness.Library, new System.Random(3));

            Step(harness, build, 0.8);
            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Entity == door.Id);
            build.Advance(10);
            Assert.Equal(harness.Level.World.Entities.Count - 2, build.Sounds.Count);
        }

        /// <summary>Verifies the landing spring follows the current entity position when gameplay moves it.</summary>
        [Fact]
        public void TheLandingSpringFollowsTheEntity()
        {
            (SceneHarness harness, LevelBuild build) = Start();
            build.Advance(10);
            harness.Level.World.Rotate(Core.Grid.WorldRotationDirection.Right);

            harness.Presenter.Update(harness.Level.World, [], 0, build: build);

            Assert.All(harness.Presenter.Sprites.Where(sprite => sprite.Page == "SpriteObjs"), sprite =>
            {
                Entity entity = harness.Level.World.GetEntity(sprite.Entity);
                Assert.InRange(sprite.X - entity.Position.X, -0.063, 0.063);
                Assert.InRange(sprite.Y - entity.Position.Y, -0.067, 0.067);
            });
        }

        private static (SceneHarness Harness, LevelBuild Build) Start()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ORock01", 2, 1), ("ORock01", 5, 1)));
            return (harness, new LevelBuild(harness.Level.World, SceneHarness.Library, new System.Random(3)));
        }

        private static void Step(SceneHarness harness, LevelBuild build, double seconds)
        {
            for (double elapsed = 0; elapsed < seconds - 1e-9; elapsed += 0.016)
            {
                double step = System.Math.Min(0.016, seconds - elapsed);
                build.Advance(step);
                harness.Presenter.Update(harness.Level.World, [], step, build: build);
            }
        }
    }
}
