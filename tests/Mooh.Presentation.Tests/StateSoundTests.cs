using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks sounds when animation state identities persist or resume after ice.</summary>
    public sealed class StateSoundTests
    {
        /// <summary>A continuing walk invokes the state's step handler at each cell arrival.</summary>
        [Fact]
        public void EasyWalkingSoundsTheNextCellWithoutRestartingItsAnimation()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1), DifficultySpeeds.DifficultyZero);
            harness.Input(new PlayerInputState(false, false, true, false));
            harness.Step(0.01);
            harness.StepUntil(() => harness.Player.Motion is not null, limitSeconds: 1);
            int first = harness.Player.Position.X;
            harness.StepUntil(() => harness.Player.Position.X != first, limitSeconds: 1);
            Assert.Contains(harness.LastEvents, item => item is PlayerMoveStarted);
            Assert.Contains(harness.Presenter.Sounds, cue => cue.Alias == "StepsMetal");
            Assert.True(harness.Animator(harness.Player).Animation.Position > 0);
        }

        /// <summary>The thaw frame cannot play a strike cue from the frozen animation's old phase.</summary>
        [Theory]
        [InlineData(AnimationLibrary.GreenMonsterTable, 0.375, "GreenStrike")]
        [InlineData(AnimationLibrary.BlueMonsterTable, 0.450, "BlueStrike")]
        public void ThawRestartsBeforeAdvancingTheFrozenPhase(string table, double firstPhase, string strike)
        {
            WorldGrid world = new(3, 3);
            Entity monster = new(EntityId.New(), new GridPosition(1, 1), InteractionMask.BlocksMovement);
            world.Add(monster);
            ResumableMonster animator = new(SceneHarness.Library.Table(table));
            animator.Update(world, monster, [], 0);
            animator.Update(world, monster, [], firstPhase - 0.001);
            _ = animator.TakeSounds();
            animator.Frozen = true;
            animator.Update(world, monster, [], 0.016);
            animator.Frozen = false;
            animator.Update(world, monster, [], 0.016);
            Assert.DoesNotContain(strike, animator.TakeSounds());
            Assert.Equal("st_strleft", animator.Animation.State);
            animator.Update(world, monster, [], firstPhase - 0.001);
            Assert.DoesNotContain(strike, animator.TakeSounds());
            animator.Update(world, monster, [], 0.002);
            Assert.Contains(strike, animator.TakeSounds());
        }

        private sealed class ResumableMonster(AnimationTable table) : MonsterAnimator(table, new Random(1), ["ice", "ice", "ice", "ice"])
        {
            public bool Frozen
            {
                get; set;
            }
            protected override bool IsFrozen(Entity entity)
            {
                return Frozen;
            }

            protected override Direction Facing(Entity entity)
            {
                return Direction.Left;
            }

            protected override string? MonsterState(Entity entity)
            {
                return "st_strleft";
            }
        }
    }
}
