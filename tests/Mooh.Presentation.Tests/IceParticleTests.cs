using System;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Exercises the ice effects through pickups and real monster death paths.</summary>
    public sealed class IceParticleTests
    {
        /// <summary>Freezing either monster family emits thirty additive snow glints.</summary>
        [Theory]
        [InlineData("MGrR")]
        [InlineData("MBlueR")]
        public void FreezingEmitsSnow(string type)
        {
            SceneHarness harness = FrozenLevel(type);

            SceneSprite[] snow = Parts(harness, "PSnow");
            Assert.Equal(30, snow.Length);
            Assert.All(snow, part => Assert.True(part.Additive));
            Assert.Empty(Parts(harness, "PBonIce"));
        }

        /// <summary>Strikes, impacts and blasts shatter either frozen family into ice and glints.</summary>
        [Theory]
        [InlineData("MGrR", "strike")]
        [InlineData("MBlueR", "strike")]
        [InlineData("MGrR", "impact")]
        [InlineData("MBlueR", "impact")]
        [InlineData("MGrR", "blast")]
        [InlineData("MBlueR", "blast")]
        public void FrozenDeathsEmitIceInsteadOfMonsterPieces(string type, string cause)
        {
            SceneHarness harness = FrozenLevel(type);
            Entity monster = harness.Level.GetEntity(201);
            harness.Step(3);
            Assert.Empty(Parts(harness, "PSnow"));

            switch (cause)
            {
                case "strike":
                    harness.Input(new PlayerInputState(false, false, true, false));
                    harness.StepUntil(() => harness.Player.Position.X == monster.Position.X - 1);
                    harness.Input(new PlayerInputState(false, false, false, false, Fire: true));
                    break;
                case "impact":
                    harness.Level.World.Add(new Entity(EntityId.New(),
                        monster.Position.Offset(Direction.Up).Offset(Direction.Up),
                        InteractionMask.BlocksMovement | InteractionMask.MovementLocked,
                        new GravityBody(2, canSlide: false)));
                    break;
                case "blast":
                    Entity bomb = new(EntityId.New(), monster.Position.Offset(Direction.Up),
                        InteractionMask.None,
                        explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
                    harness.Level.World.Add(bomb);
                    harness.Detonate(bomb);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cause));
            }

            harness.StepUntil(() => harness.LastEvents.OfType<EntityDestroyed>().Any(item => item.EntityId == monster.Id));

            Assert.Equal(20, Parts(harness, "PBonIce").Length);
            Assert.All(Parts(harness, "PBonIce"), part => Assert.False(part.Additive));
            Assert.Equal(10, Parts(harness, "PSnow").Length);
            Assert.All(Parts(harness, "PSnow"), part => Assert.True(part.Additive));
            harness.Step(3);
            Assert.Empty(Parts(harness, "PBonIce"));
            Assert.Empty(Parts(harness, "PSnow"));
        }

        /// <summary>Thawing uses the same snow burst as freezing.</summary>
        [Theory]
        [InlineData("MGrR")]
        [InlineData("MBlueR")]
        public void ThawingEmitsSnow(string type)
        {
            SceneHarness harness = FrozenLevel(type);
            harness.Step(46);

            Assert.Contains(harness.LastEvents, item => item is EnemyThawed);
            Assert.Equal(30, Parts(harness, "PSnow").Length);
            Assert.Empty(Parts(harness, "PBonIce"));
        }

        private static SceneHarness FrozenLevel(string type)
        {
            LevelDocument source = SceneHarness.BuildLevel(1, ("BIce", 2, 1), (type, 5, 1));
            source = source with
            {
                Size = new LevelSize(8, 6),
                Objects = [.. source.Objects!.Select(item => item with
                {
                    Position = new LevelPosition(item.Position.X, item.Position.Y + 2)
                })]
            };
            SceneHarness harness = new(source);
            harness.Input(new PlayerInputState(false, false, true, false));
            harness.StepUntil(() => harness.LastEvents.OfType<EnemyFrozen>().Any());
            harness.Input(default);
            Assert.True(harness.Level.GetEntity(201).IsFrozenMonster);
            return harness;
        }

        private static SceneSprite[] Parts(SceneHarness harness, string prefix)
        {
            return [.. harness.Presenter.Sprites.Where(part => part.Page == "SpriteParts"
                && part.Frame.StartsWith(prefix, StringComparison.Ordinal))];
        }
    }
}
