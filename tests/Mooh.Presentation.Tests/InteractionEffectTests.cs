using System;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Simulation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies recovered impact and spawned-beetle collection effects.</summary>
    public sealed class InteractionEffectTests
    {
        /// <summary>Enemy hits flash at each family's recovered contact point in all four directions.</summary>
        [Theory]
        [InlineData("MGrL", 4, 3, -43, -3)]
        [InlineData("MGrT", 3, 4, -14, -51)]
        [InlineData("MGrR", 2, 3, 54, 0)]
        [InlineData("MGrB", 3, 2, -16, 40)]
        [InlineData("MBlueL", 4, 3, -48, 8)]
        [InlineData("MBlueT", 3, 4, -13, -51)]
        [InlineData("MBlueR", 2, 3, 47, -9)]
        [InlineData("MBlueB", 3, 2, 13, 50)]
        public void EnemyStrikeEmitsItsFamilyContactGlow(string type, int x, int y, int pixelX, int pixelY)
        {
            LevelDocument source = SceneHarness.BuildLevel(1, (type, x, y));
            source = source with
            {
                Size = new LevelSize(8, 8),
                Objects = [.. source.Objects!.Where(item => item.Id is 1 or 200).Select(item => item.Id == 1
                    ? item with { Position = new LevelPosition(3, 3) } : item)]
            };
            SceneHarness harness = new(source);
            harness.StepUntil(() => harness.LastEvents.OfType<EnemyStrike>().Any());

            SceneSprite glow = Assert.Single(harness.Presenter.Sprites, sprite => sprite.Frame == "OGlowStr01");
            Assert.Equal(x + (pixelX / 80.0), glow.X, 10);
            Assert.Equal(y + (pixelY / 75.0), glow.Y, 10);
        }

        /// <summary>A beetle consumed in its spawn frame still has a collection position and sound.</summary>
        [Theory]
        [InlineData(Core.Enemies.SmallBeetleColor.Green)]
        [InlineData(Core.Enemies.SmallBeetleColor.Blue)]
        public void SameFrameBeetleSpawnAndCollectionKeepsItsEffect(Core.Enemies.SmallBeetleColor color)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            Core.Entities.EntityId beetle = Core.Entities.EntityId.New();
            harness.Presenter.Update(harness.Level.World,
                [new SmallBeetleSpawned(Core.Entities.EntityId.New(), beetle, new Core.Grid.GridPosition(3, 1), color),
                    new PlayerAte(harness.Player.Id, beetle)], 0);

            SceneSprite glow = Assert.Single(harness.Presenter.Sprites,
                sprite => sprite.Frame.StartsWith("PSmall", StringComparison.Ordinal));
            Assert.Equal((3.0, 1.0), (glow.X, glow.Y));
            Assert.Equal(10, harness.Presenter.Sprites.Count(sprite => sprite.Page == "SpriteObjMine"
                && sprite.Frame.StartsWith("Star", StringComparison.Ordinal)));
            Assert.Contains(harness.Presenter.Sounds, sound => sound.Alias == "SmallGet" && sound.X == 3 && sound.Y == 1);
        }

        /// <summary>A falling rock striking either enemy family emits the overlay at the contact edge.</summary>
        [Theory]
        [InlineData("MGrL")]
        [InlineData("MBlueL")]
        public void FallingRockEnemyImpactEmitsTheContactGlow(string type)
        {
            LevelDocument source = SceneHarness.BuildLevel(1, (type, 5, 1), ("ORock01", 5, -1),
                ("CellsMetal01", 4, 1), ("CellsMetal01", 6, 1));
            source = source with
            {
                Size = new LevelSize(8, 6),
                Objects = [.. source.Objects!.Select(item => item with
                {
                    Position = new LevelPosition(item.Position.X, item.Position.Y + 2)
                })]
            };
            SceneHarness harness = new(source);
            Core.Entities.EntityId monster = harness.Level.GetEntity(200).Id;

            harness.StepUntil(() => harness.LastEvents.OfType<EntityDestroyed>().Any(item => item.EntityId == monster));

            SceneSprite glow = Assert.Single(harness.Presenter.Sprites, sprite => sprite.Frame == "OGlowStr01");
            Assert.Equal((5.0, 2.5), (glow.X, glow.Y));
            Assert.True(glow.Additive);
        }

        /// <summary>A beetle spawned by either enemy family bursts into a glow and ten stars when absorbed.</summary>
        [Theory]
        [InlineData("MGrL")]
        [InlineData("MBlueL")]
        public void AbsorbingASpawnedBeetleEmitsItsCollectionEffect(string type)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, (type, 5, 1), ("WBomb", 5, 1)));
            harness.Step(0.016);
            harness.Detonate(harness.Level.GetEntity(201));
            harness.StepUntil(() => harness.LastEvents.OfType<PlayerAte>().Any());

            SceneSprite glow = Assert.Single(harness.Presenter.Sprites,
                sprite => sprite.Page == "SpriteGlow" && sprite.Frame.StartsWith("PSmall", StringComparison.Ordinal));
            SceneSprite[] stars = [.. harness.Presenter.Sprites.Where(sprite => sprite.Page == "SpriteObjMine"
                && sprite.Frame.StartsWith("Star", StringComparison.Ordinal))];
            Assert.Equal(10, stars.Length);
            Assert.True(glow.Additive);
            Assert.All(stars, star =>
            {
                Assert.True(star.Additive);
                Assert.InRange(star.X, glow.X - 0.3, glow.X + 0.3);
                Assert.InRange(star.Y, glow.Y - 0.4, glow.Y + 0.4);
            });
            Assert.Contains(harness.Presenter.Sounds, sound => sound.Alias == "SmallGet");
            Assert.DoesNotContain(harness.Presenter.Sounds, sound => sound.Alias == "CellBreak");
            harness.Step(3);
            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Frame.StartsWith("PSmall", StringComparison.Ordinal));
        }

        /// <summary>An impact emits a stationary additive overlay at the reported contact point for five frames.</summary>
        [Fact]
        public void ShakeImpactDrawsTheRecoveredOverlayAtItsContactPoint()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Presenter.Update(harness.Level.World, [new ShakeImpact(harness.Player.Id, 3.25, 1.5)], 0);

            SceneSprite glow = Assert.Single(harness.Presenter.Sprites, sprite => sprite.Frame == "OGlowStr01");
            Assert.Equal((3.25, 1.5), (glow.X, glow.Y));
            Assert.True(glow.Additive);
            Assert.Equal(int.MaxValue, glow.Pass);
            harness.Presenter.Update(harness.Level.World, [], 0.025);
            Assert.Contains(harness.Presenter.Sprites, sprite => sprite.Frame == "OGlowStr02" && sprite.X == 3.25 && sprite.Y == 1.5);
            harness.Presenter.Update(harness.Level.World, [], 0.1);
            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Frame.StartsWith("OGlowStr", StringComparison.Ordinal));
        }
    }
}
