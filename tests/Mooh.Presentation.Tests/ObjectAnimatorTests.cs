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
        /// Verifies a weapon pickup draws its glow additively right after itself, stepping with the
        /// game's clock at <c>GlowAnm</c>'s 0.02 frames per millisecond.
        /// </summary>
        [Fact]
        public void AWeaponPickupDrawsItsGlow()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 5, 1)));

            harness.Step(0.120);

            List<SceneSprite> sprites = [.. harness.Presenter.Sprites];
            int bomb = sprites.FindIndex(sprite => sprite.Entity == Placed(harness).Id && sprite.Page == "SpriteObjBombs" && !sprite.Additive);
            Assert.Equal(new SceneSprite(Placed(harness).Id, "SpriteObjBombs", "GlowGr02", sprites[bomb].X, sprites[bomb].Y, 0, Additive: true), sprites[bomb + 1]);
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
