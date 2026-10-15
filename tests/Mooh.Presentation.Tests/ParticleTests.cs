using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Scene;
using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the world particles of <c>PartAddSeq</c> and the overlay parts of
    /// <c>PartsOvr.AddSeq</c> that the original's effect functions emit.
    /// </summary>
    public sealed class ParticleTests
    {
        /// <summary>
        /// Verifies a landing raises three to five puffs of walking smoke at the object's foot,
        /// <c>SfxLandPuff</c>, and a horizontal glow over them, <c>SfxLandGlow</c>.
        /// </summary>
        [Fact]
        public void ALandingRaisesPuffsAndAGlow()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ORock01", 4, 0)));

            harness.StepUntil(() => harness.LastEvents.OfType<Landed>().Any());

            List<SceneSprite> puffs = [.. Particles(harness, "PWSmoke").Where(puff => puff.X > 3)];
            Assert.InRange(puffs.Count, 3, 5);
            Assert.All(puffs, puff => Assert.InRange(puff.Y, 1.39, 1.51));
            Assert.All(puffs, puff => Assert.InRange(puff.X, 3.59, 4.41));
            SceneSprite glow = Assert.Single(harness.Presenter.Sprites, sprite => sprite.Frame.StartsWith("OGlowLandH", StringComparison.Ordinal));
            Assert.True(glow.Additive);
        }

        /// <summary>Verifies the puffs are gone once their frames have played.</summary>
        [Fact]
        public void PuffsEndWithTheirFrames()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ORock01", 4, 0)));
            harness.StepUntil(() => harness.LastEvents.OfType<Landed>().Any());

            for (int frame = 0; frame < 125; frame++)
            {
                harness.Step(0.016);
            }

            Assert.Empty(Particles(harness, "PWSmoke"));
            Assert.DoesNotContain(harness.Presenter.Sprites, sprite => sprite.Frame.StartsWith("OGlowLand", StringComparison.Ordinal));
        }

        /// <summary>Verifies a shown blast leaves five puffs of smoke, as <c>SfxExplAdd1x1</c> does.</summary>
        [Fact]
        public void ABlastLeavesSmoke()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 5, 1)));
            harness.Step(0.016);

            harness.Detonate(harness.Level.World.Entities.Single(entity => entity.ArchivalIdentity?.ArchivalType == "WBomb"));
            harness.StepUntil(() => harness.LastEvents.OfType<ExplosionCellReached>().Any());

            Assert.True(Particles(harness, "PSmoke").Count >= 5);
        }

        /// <summary>
        /// Verifies a blasted coin bursts into its own pieces, its <c>EParts</c>, which fall under
        /// the burst's gravity.
        /// </summary>
        [Fact]
        public void ABlastedCoinBurstsIntoPieces()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OCoin01", 5, 1), ("WBomb", 4, 1)));
            harness.Step(0.016);

            harness.Detonate(harness.Level.World.Entities.Single(entity => entity.ArchivalIdentity?.ArchivalType == "WBomb"));
            harness.StepUntil(() => Particles(harness, "PCoin").Count > 0);

            Assert.InRange(Particles(harness, "PCoin").Count, 2, 4);
        }

        /// <summary>Verifies a hurt player sheds three to six hearts, <c>SfxHealth</c>.</summary>
        [Fact]
        public void AHurtPlayerShedsHearts()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Step(0.016);

            harness.Damage(10);
            harness.Step(0.016);

            Assert.InRange(Particles(harness, "PHeart").Count, 3, 6);
        }

        /// <summary>Verifies each step of a walk raises a puff at the player's feet, <c>SfxPlWalk</c>.</summary>
        [Fact]
        public void WalkingRaisesPuffsAtTheFeet()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.StepUntil(() => Particles(harness, "PWSmoke").Count > 0, limitSeconds: 1);

            Assert.All(Particles(harness, "PWSmoke"), puff => Assert.InRange(puff.Y, 1.39, 1.51));
        }

        /// <summary>
        /// Verifies an eaten coin throws sparks from its visible pixels, <c>SfxGetStars</c>: one
        /// per solid point of a 7 px grid over its frame, flying out from the cell's middle.
        /// </summary>
        [Fact]
        public void AnEatenCoinThrowsSparksFromItsPixels()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OCoin01", 2, 1)), masks: new SolidMask());
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.StepUntil(() => harness.LastEvents.OfType<PickupCollected>().Any(), limitSeconds: 2);

            List<SceneSprite> sparks = Particles(harness, "PGetStar");
            Assert.Equal(9, sparks.Count);
            Assert.All(sparks, spark => Assert.True(spark.Additive));
            Assert.Contains(sparks, spark => Math.Abs(spark.X - (2 - (10 / 80.0))) < 0.02 && Math.Abs(spark.Y - (1 - (10 / 75.0))) < 0.02);
        }

        /// <summary>
        /// Verifies a diamond twinkles from the level's start, its <c>_Prt_Diam1</c> run by
        /// <c>GameInit</c>: a gem glint or, about three times in ten, a mine-star flash, near it.
        /// </summary>
        [Fact]
        public void ADiamondTwinklesFromTheStart()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ODiam01", 5, 1)));

            harness.Step(0.016);

            SceneSprite glint = Assert.Single(harness.Presenter.Sprites, sprite => sprite.Page is "SpriteObjDiam" or "SpriteObjMine" && sprite.Pass == int.MaxValue);
            Assert.InRange(glint.X, 4.69, 5.31);
            Assert.InRange(glint.Y, 0.59, 1.41);
        }

        /// <summary>Verifies the twinkle comes back every 750 to 1500 ms.</summary>
        [Fact]
        public void ADiamondTwinklesAgain()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("ODiam01", 5, 1)));
            int glints = 0;

            for (int frame = 0; frame < 200; frame++)
            {
                harness.Step(0.016);
                glints = Math.Max(glints, harness.Presenter.Sprites.Count(sprite => sprite.Page is "SpriteObjDiam" or "SpriteObjMine" && sprite.Pass == int.MaxValue));
            }

            Assert.True(glints >= 1);
            Assert.InRange(TwinklesIn(harness, 3.2), 2, 5);
        }

        /// <summary>
        /// Verifies cola protection starts with a burst of forty bubbles, <c>_Prt_ColaProt</c>,
        /// and keeps bubbling, <c>_Prt_ColaOn</c>.
        /// </summary>
        [Fact]
        public void ColaBurstsIntoBubbles()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("BCola", 2, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.StepUntil(() => harness.Player.PlayerBody!.Progress.ColaRemaining > TimeSpan.Zero, limitSeconds: 4);

            Assert.True(harness.Presenter.Sprites.Count(sprite => sprite.Page == "SpriteObjCola") >= 40);
        }

        private static int TwinklesIn(SceneHarness harness, double seconds)
        {
            int started = 0;
            HashSet<(double, double)> seen = [];
            for (double elapsed = 0; elapsed < seconds; elapsed += 0.016)
            {
                harness.Step(0.016);
                foreach (SceneSprite sprite in harness.Presenter.Sprites.Where(sprite => sprite.Page is "SpriteObjDiam" or "SpriteObjMine" && sprite.Pass == int.MaxValue))
                {
                    if (seen.Add((Math.Round(sprite.X, 3), Math.Round(sprite.Y, 3))))
                    {
                        started++;
                    }
                }
            }

            return started;
        }

        private static List<SceneSprite> Particles(SceneHarness harness, string prefix)
        {
            return [.. harness.Presenter.Sprites.Where(sprite => sprite.Page == "SpriteParts" && sprite.Frame.StartsWith(prefix, StringComparison.Ordinal))];
        }

        /// <summary>A mask whose every frame is a solid 20 by 20 square around its anchor.</summary>
        private sealed class SolidMask : ISpriteMask
        {
            public SpriteFrame? FrameOf(string page, string frame)
            {
                return new SpriteFrame(frame, 0, 0, 20, 20, -10, -10);
            }

            public bool IsSolid(string page, int x, int y)
            {
                return true;
            }
        }
    }
}
