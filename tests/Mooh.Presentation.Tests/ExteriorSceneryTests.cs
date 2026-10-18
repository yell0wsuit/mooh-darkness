using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Simulation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies runtime scenery fills the exterior without changing the puzzle.</summary>
    public sealed class ExteriorSceneryTests
    {
        /// <summary>Even a single-material level gets solid accents and connected pipe runs in its exterior.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void ExteriorIncludesVariedSolidsAndConnectedPipes(int turns)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            for (int turn = 0; turn < turns; turn++)
            {
                scenery.Rotate(WorldRotationDirection.Left);
            }
            SceneCamera camera = new()
            {
                HorizontalMargin = 2000,
                VerticalMargin = 2000
            };
            camera.Snap(1, 1);
            Dictionary<(double X, double Y), SceneSprite> cells = scenery.Layout(camera).ToDictionary(sprite => (sprite.X, sprite.Y));
            Assert.Contains(cells.Values, sprite => sprite.Page == "SpriteCellWood");
            Assert.Contains(cells.Values, sprite => sprite.Page == "SpriteCellJungle");
            SceneSprite[] pipes = [.. cells.Values.Where(sprite => sprite.Page == "SpriteCellWoodPipes"
                && Math.Abs(sprite.X) < 20 && Math.Abs(sprite.Y) < 20)];
            Assert.NotEmpty(pipes);
            foreach (SceneSprite pipe in pipes)
            {
                bool horizontal = pipe.Frame.StartsWith("OHor", StringComparison.Ordinal);
                Assert.Equal(1, pipe.Pass);
                Assert.True(Enumerable.Range(-2, 3).Any(start => Enumerable.Range(start, 3).All(offset =>
                    cells.TryGetValue((pipe.X + (horizontal ? offset : 0), pipe.Y + (horizontal ? 0 : offset)), out SceneSprite neighbor)
                    && neighbor.Page == pipe.Page
                    && neighbor.Frame.StartsWith(horizontal ? "OHor" : "OVert", StringComparison.Ordinal))),
                    $"Disconnected pipe at ({pipe.X}, {pipe.Y}) after {turns} turns.");
            }
            Assert.All(cells.Values, sprite => Assert.True(ArchivalObjectCatalog.Get(TypeOf(sprite)).Interactions.HasFlag(InteractionMask.BlocksMovement)));
        }

        /// <summary>Real gravity must leave each decorative rock stationary in its generated neighborhood.</summary>
        [Theory]
        [InlineData("CellsMetal01", 0)]
        [InlineData("CellsMetal01", 1)]
        [InlineData("CellsMetal01", 2)]
        [InlineData("CellsMetal01", 3)]
        [InlineData("CellsEat01", 0)]
        [InlineData("CellsSolid01", 0)]
        public void GeneratedRocksRemainStationaryUnderRealGravity(string terrainType, int turns)
        {
            LevelDocument source = SceneHarness.BuildLevel(1);
            SceneHarness harness = new(source with
            {
                Objects = [.. source.Objects!.Select(item => item.Type == "CellsMetal01" ? item with { Type = terrainType } : item)]
            });
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            for (int turn = 0; turn < turns; turn++)
            {
                scenery.Rotate(WorldRotationDirection.Left);
            }
            SceneCamera camera = new()
            {
                HorizontalMargin = 1600,
                VerticalMargin = 1600
            };
            camera.Snap(1, 1);
            Dictionary<(double X, double Y), SceneSprite> cells = scenery.Layout(camera).ToDictionary(sprite => (sprite.X, sprite.Y));
            SceneSprite[] rocks = [.. cells.Values.Where(sprite => sprite.Frame.StartsWith("ORock", StringComparison.Ordinal)
                && Math.Abs(sprite.X) < 15 && Math.Abs(sprite.Y) < 15)];
            Assert.NotEmpty(rocks);
            foreach (SceneSprite rock in rocks)
            {
                WorldGrid patch = new(3, 3);
                Entity body = new(EntityId.New(), new GridPosition(1, 1), ArchivalObjectCatalog.Get("ORock01").Interactions,
                    new GravityBody(1, canSlide: true));
                patch.Add(body);
                for (int dy = 0; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || !cells.TryGetValue((rock.X + dx, rock.Y + dy), out SceneSprite neighbor))
                        {
                            continue;
                        }
                        string type = TypeOf(neighbor);
                        patch.Add(new Entity(EntityId.New(), new GridPosition(dx + 1, dy + 1), ArchivalObjectCatalog.Get(type).Interactions));
                    }
                }
                GravitySystem gravity = new(new DefaultGravityCollisionPolicy(), new Random(0));
                List<SimulationEvent> events = [];
                gravity.EvaluateStationary(patch, Direction.Down, events);

                Assert.Empty(events);
                Assert.Null(body.Motion);
                Assert.Equal(new GridPosition(1, 1), body.Position);
            }
        }

        /// <summary>The user's shelf and flat dirt/solid support patterns need no walls beside the rock.</summary>
        [Theory]
        [InlineData("CellsMetal01")]
        [InlineData("CellsEat01")]
        [InlineData("CellsSolid01")]
        public void StableSupportsDoNotNeedSideWalls(string terrainType)
        {
            LevelDocument source = SceneHarness.BuildLevel(1);
            SceneHarness harness = new(source with
            {
                Objects = [.. source.Objects!.Select(item => item.Type == "CellsMetal01" ? item with { Type = terrainType } : item)]
            });
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            SceneCamera camera = new()
            {
                HorizontalMargin = 1600,
                VerticalMargin = 1600
            };
            camera.Snap(1, 1);
            Dictionary<(double X, double Y), SceneSprite> cells = scenery.Layout(camera).ToDictionary(sprite => (sprite.X, sprite.Y));

            Assert.Contains(cells.Values, rock => rock.Frame.StartsWith("ORock", StringComparison.Ordinal)
                && Math.Abs(rock.X) < 15 && Math.Abs(rock.Y) < 15
                && !cells.ContainsKey((rock.X - 1, rock.Y)) && !cells.ContainsKey((rock.X + 1, rock.Y))
                && cells.TryGetValue((rock.X, rock.Y + 1), out SceneSprite support) && TypeOf(support) == terrainType
                && (terrainType != "CellsMetal01"
                    || (cells.TryGetValue((rock.X - 1, rock.Y + 1), out SceneSprite left) && left.Page == "SpriteCellMetal"
                        && cells.TryGetValue((rock.X + 1, rock.Y + 1), out SceneSprite right) && right.Page == "SpriteCellMetal")));
        }

        /// <summary>Unsupported roll-off routes receive dirt instead of a decorative rock.</summary>
        [Fact]
        public void UnsafeMetalEdgesUseDirt()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 0);
            SceneCamera camera = new()
            {
                HorizontalMargin = 1600,
                VerticalMargin = 1600
            };
            camera.Snap(1, 1);
            Dictionary<(double X, double Y), SceneSprite> cells = scenery.Layout(camera).ToDictionary(sprite => (sprite.X, sprite.Y));
            Assert.Contains(cells.Values, dirt => dirt.Page == "SpriteCellNrm"
                && cells.TryGetValue((dirt.X, dirt.Y + 1), out SceneSprite support) && support.Page == "SpriteCellMetal"
                && ((!cells.ContainsKey((dirt.X - 1, dirt.Y)) && !cells.ContainsKey((dirt.X - 1, dirt.Y + 1)))
                    || (!cells.ContainsKey((dirt.X + 1, dirt.Y)) && !cells.ContainsKey((dirt.X + 1, dirt.Y + 1)))));
        }

        /// <summary>Every metal-supported rock has both possible roll-off routes blocked.</summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(42, 0)]
        [InlineData(12345, 0)]
        [InlineData(42, 1)]
        [InlineData(42, 2)]
        [InlineData(42, 3)]
        public void MetalSupportedRocksCannotRollOff(int seed, int turns)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, seed);
            for (int turn = 0; turn < turns; turn++)
            {
                scenery.Rotate(WorldRotationDirection.Left);
            }
            SceneCamera camera = new()
            {
                HorizontalMargin = 1600,
                VerticalMargin = 1600
            };
            camera.Snap(1, 1);
            Dictionary<(double X, double Y), SceneSprite> cells = scenery.Layout(camera).ToDictionary(sprite => (sprite.X, sprite.Y));
            SceneSprite[] rocks = [.. cells.Values.Where(sprite => sprite.Frame.StartsWith("ORock", StringComparison.Ordinal)
                && Math.Abs(sprite.X) < 15 && Math.Abs(sprite.Y) < 15)];
            foreach (SceneSprite rock in rocks)
            {
                if (!cells.TryGetValue((rock.X, rock.Y + 1), out SceneSprite support)
                    || !ArchivalObjectCatalog.Get(TypeOf(support)).Interactions.HasFlag(InteractionMask.RoundSupport))
                {
                    continue;
                }
                foreach (int side in new[] { -1, 1 })
                {
                    Assert.True(cells.ContainsKey((rock.X + side, rock.Y)) || cells.ContainsKey((rock.X + side, rock.Y + 1)),
                        $"Rock at ({rock.X}, {rock.Y}) can roll off its support (seed {seed}).");
                }
            }
        }

        /// <summary>Decorative rocks sit on terrain, including after gravity-relative world turns.</summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(42, 0)]
        [InlineData(12345, 0)]
        [InlineData(42, 1)]
        [InlineData(42, 2)]
        [InlineData(42, 3)]
        public void RocksRestOnTerrain(int seed, int turns)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, seed);
            for (int turn = 0; turn < turns; turn++)
            {
                scenery.Rotate(WorldRotationDirection.Left);
            }

            SceneCamera camera = new()
            {
                HorizontalMargin = 1000,
                VerticalMargin = 1000
            };
            camera.Snap(1, 1);
            SceneSprite[] rocks = [.. scenery.Layout(camera).Where(sprite => sprite.Frame.StartsWith("ORock", StringComparison.Ordinal))];
            camera.HorizontalMargin = 1200;
            camera.VerticalMargin = 1200;
            Dictionary<(double X, double Y), SceneSprite> surrounding = scenery.Layout(camera).ToDictionary(sprite => (sprite.X, sprite.Y));

            Assert.NotEmpty(rocks);
            foreach (SceneSprite rock in rocks)
            {
                Assert.True(surrounding.TryGetValue((rock.X, rock.Y + 1), out SceneSprite support),
                    $"Rock at ({rock.X}, {rock.Y}) has no support after {turns} turns (seed {seed}).");
                Assert.True(ArchivalObjectCatalog.Get(TypeOf(support)).Interactions.HasFlag(InteractionMask.BlocksMovement));
            }
        }

        /// <summary>Scenery is included in the published frame and follows a turn in flight.</summary>
        [Fact]
        public void PresenterIncludesSceneryInWorldRotation()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            harness.Presenter.Scenery = scenery;
            SceneCamera camera = new();
            camera.Snap(1, 1);
            WorldSpin spin = new(new GridPosition(1, 1), 0.5);
            harness.Presenter.Update(harness.Level.World, [], 0, spin, camera: camera);
            foreach (SceneSprite sprite in scenery.Layout(camera, spin))
            {
                (double x, double y) = spin.Transform(sprite.X, sprite.Y);
                Assert.Contains(sprite with
                {
                    X = x,
                    Y = y
                }, harness.Presenter.Sprites);
            }

            harness.Level.World.Rotate(WorldRotationDirection.Left);
            harness.Presenter.Update(harness.Level.World,
                [new WorldRotationCompleted(harness.Player.Id, WorldRotationDirection.Left, 4, 8)], 0, camera: camera);
            Assert.All(harness.Presenter.Sprites.Where(sprite => sprite.Entity == default
                    && (sprite.Page == "SpriteCellMetal" || sprite.Frame.StartsWith("ORock", StringComparison.Ordinal))),
                sprite => Assert.False(harness.Level.World.Contains(new GridPosition((int)sprite.X, (int)sprite.Y))));
        }

        /// <summary>A rotating view still includes every generated cell whose anchor is visible.</summary>
        [Theory]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        [InlineData(1.0)]
        public void InFlightRotationCoversVisibleExterior(double angle)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            SceneCamera broad = new()
            {
                HorizontalMargin = 2000,
                VerticalMargin = 2000
            };
            SceneCamera camera = new()
            {
                HorizontalMargin = 600,
                VerticalMargin = 200
            };
            broad.Snap(1, 1);
            camera.Snap(1, 1);
            WorldSpin spin = new(new GridPosition(1, 1), angle);
            IReadOnlyList<SceneSprite> visible = scenery.Layout(camera, spin);
            foreach (SceneSprite sprite in scenery.Layout(broad))
            {
                (double x, double y) = spin.Transform(sprite.X, sprite.Y);
                (double screenX, double screenY) = camera.ToScreen(x, y);
                if (screenX >= -600 && screenX <= 1400 && screenY >= -200 && screenY <= 800)
                {
                    Assert.Contains(sprite, visible);
                }
            }
        }

        /// <summary>Both extended viewport axes receive terrain and scattered rocks.</summary>
        [Theory]
        [InlineData(1000, 0)]
        [InlineData(0, 1000)]
        public void FillsOnlyExteriorWithoutChangingWorld(double horizontal, double vertical)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            WorldGrid world = harness.Level.World;
            Entity[] original = [.. world.Entities];
            ExteriorScenery scenery = new(world, SceneHarness.Library, 42);
            SceneCamera camera = new()
            {
                HorizontalMargin = horizontal,
                VerticalMargin = vertical
            };
            camera.Snap(1, 1);

            IReadOnlyList<SceneSprite> sprites = scenery.Layout(camera);

            Assert.NotEmpty(sprites);
            Assert.All(sprites, sprite => Assert.False(world.Contains(new GridPosition((int)sprite.X, (int)sprite.Y))));
            Assert.Contains(sprites, sprite => sprite.Page == "SpriteCellMetal");
            Assert.Contains(sprites, sprite => sprite.Page != "SpriteCellMetal");
            Assert.Equal(original, world.Entities);
            Assert.Equal(8, world.Width);
            Assert.Equal(4, world.Height);
        }

        /// <summary>Resizing and camera movement do not reroll overlapping exterior cells.</summary>
        [Fact]
        public void ExistingCellsSurviveResizeAndCameraMovement()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            SceneCamera camera = new();
            camera.Snap(1, 1);
            SceneSprite[] first = [.. scenery.Layout(camera)];
            camera.HorizontalMargin = 1200;
            camera.VerticalMargin = 1200;
            SceneSprite[] expanded = [.. scenery.Layout(camera)];
            Assert.All(first, sprite => Assert.Contains(sprite, expanded));
            camera.Snap(2, 1);
            SceneSprite[] moved = [.. scenery.Layout(camera)];
            Assert.All(first, sprite => Assert.Contains(sprite, moved));
            camera.HorizontalMargin = 0;
            camera.VerticalMargin = 0;
            camera.Snap(1, 1);
            Assert.Equal(first, scenery.Layout(camera));
        }

        /// <summary>A completed turn keeps the terrain's material and frame; decorative rocks follow gravity.</summary>
        [Theory]
        [InlineData(WorldRotationDirection.Left, 1)]
        [InlineData(WorldRotationDirection.Right, 1)]
        [InlineData(WorldRotationDirection.Left, 4)]
        [InlineData(WorldRotationDirection.Right, 4)]
        public void CompletedRotationPreservesScenery(WorldRotationDirection direction, int turns)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            ExteriorScenery scenery = new(harness.Level.World, SceneHarness.Library, 42);
            SceneCamera camera = new()
            {
                HorizontalMargin = 1000,
                VerticalMargin = 1000
            };
            camera.Snap(2, 2);
            SceneSprite[] before = [.. scenery.Layout(camera).Where(sprite => sprite.Page == "SpriteCellMetal"
                && Math.Abs(sprite.X) < 5 && Math.Abs(sprite.Y) < 5)];
            for (int turn = 0; turn < turns; turn++)
            {
                scenery.Rotate(direction);
            }
            IReadOnlyList<SceneSprite> after = scenery.Layout(camera);
            foreach (SceneSprite sprite in before)
            {
                GridPosition rotated = new((int)sprite.X, (int)sprite.Y);
                (int width, int height) = (8, 4);
                for (int turn = 0; turn < turns; turn++)
                {
                    rotated = WorldRotation.Map(rotated, direction, width, height);
                    (width, height) = (height, width);
                }
                Assert.Contains(sprite with
                {
                    X = rotated.X,
                    Y = rotated.Y
                }, after);
            }
        }
        private static string TypeOf(SceneSprite sprite)
        {
            return sprite.Page switch
            {
                "SpriteCellMetal" => "CellsMetal01",
                "SpriteCellWood" => "CellsSolidWd",
                "SpriteCellWoodPipes" => sprite.Frame.StartsWith("OHor", StringComparison.Ordinal) ? "PipeHor" : "PipeVert",
                "SpriteCellNrm" => sprite.Frame.StartsWith("OCellE", StringComparison.Ordinal) ? "CellsEat01" : "CellsSolid01",
                "SpriteCellDesert" => sprite.Frame.StartsWith("OCellE", StringComparison.Ordinal) ? "DCellsEat01" : "DCellsSolid01",
                "SpriteCellJungle" => sprite.Frame.StartsWith("OCellPlant", StringComparison.Ordinal) ? "CellsSolidPl"
                    : sprite.Frame.StartsWith("OCellE", StringComparison.Ordinal) ? "JCellsEat01" : "JCellsSolid01",
                "SpriteObjs" when sprite.Frame.StartsWith("ORock", StringComparison.Ordinal) => "ORock01",
                _ => throw new InvalidOperationException($"Unexpected exterior object: {sprite.Page}/{sprite.Frame}."),
            };
        }
    }
}
