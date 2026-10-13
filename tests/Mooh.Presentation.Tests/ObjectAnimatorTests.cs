using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Player;
using Mooh.Presentation.Animation;
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
