using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Player;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the Green and Blue Monster animators show the recovered <c>CGameObjGrMon</c> and
    /// <c>CGameObjMonBlue</c> states for what the simulation's monsters are doing.
    /// </summary>
    public sealed class MonsterAnimatorTests
    {
        /// <summary>Verifies a walking Green Monster plays its walk.</summary>
        [Fact]
        public void AWalkingGreenMonsterPlaysItsWalk()
        {
            SceneHarness harness = new(Level(("MGrL", 5, 1)));

            harness.Step(0.050);

            Assert.Equal("st_wlkleft", harness.Animator(Monster(harness)).Animation.State);
        }

        /// <summary>Verifies a blocked Green Monster turns clockwise, <c>ST_ThinkRot</c>.</summary>
        [Fact]
        public void ABlockedGreenMonsterTurnsClockwise()
        {
            SceneHarness harness = new(Level(("MGrL", 5, 1), ("CellsMetal01", 4, 1)));

            harness.Step(0.050);

            Assert.Equal("st_rotl2t", harness.Animator(Monster(harness)).Animation.State);
        }

        /// <summary>Verifies a Green Monster's strike plays its wind-up, then its recovery.</summary>
        [Fact]
        public void AGreenMonsterStrikePlaysItsWindupThenItsRecovery()
        {
            SceneHarness harness = new(Level(("MGrL", 2, 1)));

            harness.Step(0.050);
            Assert.Equal("st_strleft", harness.Animator(Monster(harness)).Animation.State);

            harness.Step(0.600);
            Assert.Equal("st_str1left", harness.Animator(Monster(harness)).Animation.State);
        }

        /// <summary>
        /// Verifies a Blue Monster's strike plays its wind-up, its recovery and the turnaround its
        /// recovery chain ends in.
        /// </summary>
        [Fact]
        public void ABlueMonsterStrikeEndsInItsTurnaround()
        {
            SceneHarness harness = new(Level(("MBlueL", 2, 1)));

            harness.Step(0.050);
            Assert.Equal("st_strleft", harness.Animator(Monster(harness)).Animation.State);

            harness.Step(0.600);
            Assert.Equal("st_str_pre_left", harness.Animator(Monster(harness)).Animation.State);

            harness.Step(0.700);
            Assert.Equal("st_rotb2r", harness.Animator(Monster(harness)).Animation.State);
        }

        /// <summary>Verifies a frozen monster shows its still ice frame, <c>IceOn</c>.</summary>
        [Fact]
        public void AFrozenMonsterShowsItsIceFrame()
        {
            SceneHarness harness = new(Level(("MGrL", 6, 1), ("BIce", 2, 1)));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.Step(0.700);

            Entity monster = Monster(harness);
            Assert.True(monster.GreenMonsterBody!.IsFrozen);
            string expected = monster.GreenMonsterBody.FacingDirection switch
            {
                Direction.Left => "OIceLeft",
                Direction.Right => "OIceRight",
                Direction.Up => throw new System.NotImplementedException(),
                Direction.Down => throw new System.NotImplementedException(),
                _ => "OIceVert",
            };
            EntityAnimator animator = harness.Animator(monster);
            Assert.Equal(expected, animator.Frame);
            Assert.Equal("SpriteMonGreen", animator.Page);
        }

        private static Entity Monster(SceneHarness harness)
        {
            return harness.Level.GetEntity(200);
        }

        private static LevelDocument Level(params (string Type, int X, int Y)[] extra)
        {
            return SceneHarness.BuildLevel(playerY: 1, extra);
        }
    }
}
