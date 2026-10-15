using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Player;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies placed and spawned objects show their recovered <c>CGameObjFallBase</c>,
    /// <c>CGameObjFSABase</c> and cell states.
    /// </summary>
    public sealed class ObjectAnimatorTests
    {
        /// <summary>Verifies terrain shows the variant its editor index picks, <c>EInd % len</c>.</summary>
        [Fact]
        public void TerrainShowsTheVariantItsEditorIndexPicks()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, [("CellsEat01", 4, 1, 7)]));

            harness.Step(0.016);

            AnimationClip earth = SceneHarness.Library.Table("CellsEat01").States[AnimationLibrary.StillState];
            Assert.Equal(earth.Variants[7 % earth.Variants.Count][0], harness.Animator(Placed(harness)).Frame);
        }

        /// <summary>
        /// Verifies terrain draws with its random depth shift: <c>ObjRepMake</c> picks
        /// <c>fShZ</c> in <c>CellSh</c>, and the list draws it <c>CNT_WrdShZX</c> = -80 and
        /// <c>CNT_WrdShZY</c> = 40 pixels per unit away from its cell.
        /// </summary>
        [Fact]
        public void TerrainDrawsWithItsDepthShift()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("CellsEat01", 4, 1)));

            harness.Step(0.016);

            SceneSprite earth = harness.Presenter.Sprites.Single(sprite => sprite.Entity == Placed(harness).Id);
            double depth = (4 - earth.X) * 80 / 80;
            Assert.InRange(depth, -0.05, 0.05);
            Assert.NotEqual(0, depth);
            Assert.Equal(1 + (40 * depth / 75), earth.Y, 9);
        }

        /// <summary>Verifies a pickup plays its idle loop on the ordinary pass.</summary>
        [Fact]
        public void APickupPlaysItsIdleLoop()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OBubble", 4, 1)));

            harness.Step(0.016);
            string? first = harness.Animator(Placed(harness)).Frame;
            harness.Step(0.100);

            EntityAnimator animator = harness.Animator(Placed(harness));
            Assert.Equal("st_stay", animator.Animation.State);
            Assert.NotEqual(first, animator.Frame);
            Assert.Equal(0, animator.Pass);
        }

        /// <summary>
        /// Verifies a weapon pickup draws its glow additively before its shadow and body, stepping with the
        /// game's clock at <c>GlowAnm</c>'s 0.02 frames per millisecond.
        /// </summary>
        [Fact]
        public void AWeaponPickupDrawsItsGlow()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 5, 1)));

            harness.Step(0.120);

            List<SceneSprite> sprites = [.. harness.Presenter.Sprites];
            int bomb = sprites.FindIndex(sprite => sprite.Entity == Placed(harness).Id && sprite.Page == "SpriteObjBombs" && !sprite.Additive);
            int glow = sprites.FindIndex(sprite => sprite.Entity == Placed(harness).Id && sprite.Additive);
            Assert.Equal(new SceneSprite(Placed(harness).Id, "SpriteObjBombs", "GlowGr02", sprites[bomb].X, sprites[bomb].Y, 0, Additive: true), sprites[glow]);
            Assert.True(glow < bomb);
            Assert.All(sprites.Where(sprite => sprite.Entity == Placed(harness).Id && !sprite.Additive),
                sprite => Assert.True(glow < sprites.IndexOf(sprite)));
        }

        /// <summary>Pickup glows stay beneath their own sprite and below objects drawn in front.</summary>
        [Theory]
        [InlineData("ODiam01", 1)]
        [InlineData("ODiam02", 1)]
        [InlineData("ODiam03", 1)]
        [InlineData("ODiam05", 1)]
        [InlineData("ODiam01", 0)]
        [InlineData("WBomb", 1)]
        [InlineData("WGrBox", 1)]
        [InlineData("WROne", 1)]
        [InlineData("WRBox", 1)]
        [InlineData("WMinesBox", 1)]
        [InlineData("BCola", 1)]
        [InlineData("BIce", 1)]
        [InlineData("ODiam04", 1)]
        public void PickupGlowsShareTheirObjectsLayer(string type, int y)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, (type, 5, y), ("CellsMetal01", 6, 1)));
            harness.Step(0.120);

            Entity pickup = Placed(harness);
            List<SceneSprite> sprites = [.. harness.Presenter.Sprites];
            SceneSprite glow = Assert.Single(sprites, sprite => sprite.Entity == pickup.Id && sprite.Additive);
            int glowIndex = sprites.IndexOf(glow);
            SceneSprite body = Assert.Single(sprites, sprite => sprite.Entity == pickup.Id
                && !sprite.Additive && sprite.Page != "SpriteParts");
            Assert.True(glowIndex < sprites.IndexOf(body));
            Assert.All(sprites.Where(sprite => sprite.Entity == pickup.Id && !sprite.Additive),
                sprite => Assert.True(glowIndex < sprites.IndexOf(sprite)));
            Assert.Equal(pickup.Id, body.Entity);
            Assert.False(body.Additive);
            Assert.Equal(body.Pass, glow.Pass);
            Assert.Equal((body.X, body.Y), (glow.X, glow.Y));
            Assert.True(glowIndex < sprites.FindIndex(sprite => sprite.Entity == harness.Level.GetEntity(201).Id));
            if (y == 0)
            {
                Assert.NotNull(pickup.Motion);
            }
        }

        /// <summary>Coins and keys draw their synchronized glow between the shadow and body.</summary>
        [Theory]
        [InlineData("OCoin01", "Glow1_")]
        [InlineData("OCoin02", "Glow2_")]
        [InlineData("OCoin03", "Glow3_")]
        [InlineData("OKDKey1", "Glow1_")]
        [InlineData("OKDKey2", "Glow2_")]
        [InlineData("OKDKey3", "Glow3_")]
        public void CoinsAndKeysDrawTheirSynchronizedGlow(string type, string prefix)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, (type, 5, 1)));
            harness.Step(0.120);
            Entity pickup = Placed(harness);
            EntityAnimator animator = harness.Animator(pickup);
            List<SceneSprite> sprites = [.. harness.Presenter.Sprites.Where(sprite => sprite.Entity == pickup.Id)];
            SceneSprite glow = Assert.Single(sprites, sprite => sprite.Additive);
            int frame = (int)animator.Animation.Position % 20;
            Assert.Equal(prefix + frame.ToString("00", System.Globalization.CultureInfo.InvariantCulture), glow.Frame);
            SceneSprite body = sprites[^1];
            Assert.Equal(animator.Frame, body.Frame);
            Assert.Equal(glow, sprites[^2]);
            Assert.False(body.Additive);
            Assert.Equal((body.X, body.Y, body.Pass), (glow.X, glow.Y, glow.Pass));
            Assert.All(sprites.Where(sprite => sprite.Page == "SpriteParts"),
                shadow => Assert.True(sprites.IndexOf(shadow) < sprites.IndexOf(glow)));
            string initial = glow.Frame;
            harness.Step(0.175);
            Assert.NotEqual(initial, harness.Presenter.Sprites.Single(sprite => sprite.Entity == pickup.Id && sprite.Additive).Frame);
        }

        /// <summary>The burger sparkle follows the game clock independently of its random body phase.</summary>
        [Fact]
        public void FoodSparklesFollowTheGameClock()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OEat01", 5, 1)));
            harness.Step(0.120);
            Entity pickup = Placed(harness);
            Assert.Equal("P1_02", harness.Presenter.Sprites.Single(sprite => sprite.Entity == pickup.Id && sprite.Additive).Frame);
            harness.Step(1.100);
            Assert.Equal("P1_00", harness.Presenter.Sprites.Single(sprite => sprite.Entity == pickup.Id && sprite.Additive).Frame);
        }

        /// <summary>Clock-based glows continue during pause while body animations remain stopped.</summary>
        [Theory]
        [InlineData("WBomb", "GlowGr04")]
        [InlineData("OEat01", "P1_04")]
        public void ClockBasedLayersContinueWhilePhysicsIsPaused(string type, string expected)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, (type, 5, 1)));
            harness.Step(0.120);
            Entity pickup = Placed(harness);
            string? bodyFrame = harness.Animator(pickup).Frame;

            harness.Presenter.Update(harness.Level.World, [], 0, bonusSeconds: 0.100);

            Assert.Equal(expected, harness.Presenter.Sprites.Single(sprite => sprite.Entity == pickup.Id && sprite.Additive).Frame);
            Assert.Equal(bodyFrame, harness.Animator(pickup).Frame);
        }

        /// <summary>Verifies a lit balloon plays its fuse chain through the ticks.</summary>
        [Fact]
        public void ALitBalloonPlaysItsFuseChain()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OBaloon", 2, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            Entity balloon = Placed(harness);

            harness.StepUntil(() => balloon.BalloonFuseBody!.HasBeenLit);
            Assert.Equal("st_work", harness.Animator(balloon).Animation.State);

            harness.Input(default);
            harness.Step(1.0);
            Assert.Equal("st_tick12", harness.Animator(balloon).Animation.State);
        }

        /// <summary>Verifies a lit balloon cues its state's sound, <c>SndBaloonMorth</c>, where it is drawn.</summary>
        [Fact]
        public void ALitBalloonCuesItsStateSound()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OBaloon", 2, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            Entity balloon = Placed(harness);

            harness.StepUntil(() => balloon.BalloonFuseBody!.HasBeenLit);

            // The push that lit it is already moving it, so it sounds from where it is drawn.
            SoundCue cue = Assert.Single(harness.Presenter.Sounds, sound => sound.Alias == "BaloonMorph");
            Assert.InRange(cue.X, 2, 3);
            Assert.Equal(1, cue.Y);
        }

        /// <summary>Verifies a landed arrow winds up, then flies.</summary>
        [Fact]
        public void ALandedArrowWindsUpThenFlies()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OArrowR", 4, 0)));
            Entity arrow = Placed(harness);

            harness.StepUntil(() => arrow.FlyingArrowBody!.IsLaunched);
            Assert.Equal("st_work", harness.Animator(arrow).Animation.State);

            harness.StepUntil(() => arrow.FlyingArrowBody!.IsFlying);
            Assert.Equal("st_arrowfly", harness.Animator(arrow).Animation.State);
        }

        /// <summary>Verifies an opening key door plays its opening.</summary>
        [Fact]
        public void AnOpeningKeyDoorPlaysItsOpening()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OKDKey1", 2, 1), ("OKDDoor1", 3, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            Entity door = harness.Level.GetEntity(201);

            harness.StepUntil(() => door.KeyDoorBody!.OpeningRemainingSeconds is not null);

            Assert.Equal("st_work", harness.Animator(door).Animation.State);
        }

        /// <summary>Verifies a read message table shows its still read frame, over the objects.</summary>
        [Fact]
        public void AReadMessageTableShowsItsReadFrame()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OTable", 2, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            Entity table = Placed(harness);

            harness.StepUntil(() => table.MessageTableBody!.HasBeenRead);

            EntityAnimator animator = harness.Animator(table);
            Assert.Equal("OSt00", animator.Frame);
            Assert.Equal(1, animator.Pass);
        }

        /// <summary>
        /// Verifies the start door opens for the player's entry, stays open while the player
        /// walks out, then closes and goes.
        /// </summary>
        [Fact]
        public void TheStartDoorOpensThenClosesAndGoes()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Begin();
            harness.Step(0.016);
            Entity door = harness.Level.World.Entities.Single(entity => entity.DoorBody is not null);

            Assert.Equal("st_doorin", harness.Animator(door).Animation.State);
            Assert.Equal(-1, harness.Animator(door).Pass);

            harness.Step(0.800);
            Assert.Equal("st_dooropenlock", harness.Animator(door).Animation.State);

            harness.Step(1.100);
            Assert.Equal("st_doorcloseandout", harness.Animator(door).Animation.State);
        }

        /// <summary>
        /// Verifies a door kept as a shelter stays closed while the player does anything else.
        /// </summary>
        [Fact]
        public void AShelterDoorStaysClosedWhileThePlayerActsElsewhere()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Begin();
            Entity door = harness.Level.World.Entities.Single(entity => entity.DoorBody is not null);
            PlayerBody player = harness.Player.PlayerBody!;
            harness.StepUntil(() => player.Action is null);
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: false, Down: false, Fire: true));
            harness.StepUntil(() => door.DoorBody!.IsShelter);
            harness.Input(default);
            harness.StepUntil(() => player.Action is null, limitSeconds: 10);
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));
            harness.StepUntil(() => harness.Player.Position.X == 2);
            harness.Input(default);
            harness.StepUntil(() => harness.Player.Motion is null);

            harness.Input(new PlayerInputState(Left: false, Up: false, Right: false, Down: false, Fire: true));
            harness.StepUntil(() => player.Action is not null);
            harness.Step(0.500);

            Assert.Equal("st_stay", harness.Animator(door).Animation.State);
        }

        private static Entity Placed(SceneHarness harness)
        {
            return harness.Level.GetEntity(200);
        }
    }
}
