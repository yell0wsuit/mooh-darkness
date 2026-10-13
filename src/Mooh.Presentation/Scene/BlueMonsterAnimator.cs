using System;

using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>Shows the recovered <c>CGameObjMonBlue</c> state for a simulated Blue Monster.</summary>
    /// <remarks>
    /// The strike is <c>ST_Str</c> (450 ms) into <c>ST_StrDo</c> (150 ms), whose end strikes. The
    /// recovery is <c>st_str_pre_*</c> into <c>st_str*_rot</c>, and that chain ends in the quarter
    /// turn the simulation's turnaround phase times. The turnaround has already reversed the
    /// facing, so its state is read from the chain of the facing it left.
    /// </remarks>
    /// <param name="library">The recovered animation tables.</param>
    /// <param name="random">The source of the original's random phase.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public sealed class BlueMonsterAnimator(AnimationLibrary library, Random random) : MonsterAnimator((library ?? throw new ArgumentNullException(nameof(library))).Table(AnimationLibrary.BlueMonsterTable), random, IceFrames)
    {
        /// <summary>The recovered <c>IceAinms</c>, one frame per facing.</summary>
        private static readonly string[] IceFrames = ["OIceLeft", "OIceTop", "OIceRight", "OIceBottom"];

        /// <inheritdoc/>
        protected override string? MonsterState(Entity entity)
        {
            BlueMonsterBody body = Body(entity);
            Direction facing = body.FacingDirection;
            string prefix = body.IsSmall ? "st_sm" : "st_";
            return body.State switch
            {
                BlueMonsterState.Walking => prefix + "wlk" + StateNames.Name(entity.Motion?.TravelDirection ?? facing),
                BlueMonsterState.TurningLeft => StateNames.Turn(prefix, facing, facing.TurnLeft()),
                BlueMonsterState.TurningRight => StateNames.Turn(prefix, facing, facing.TurnRight()),
                BlueMonsterState.AttackWindup => "st_str" + StateNames.Name(facing),
                BlueMonsterState.AttackRecovery => "st_str_pre_" + StateNames.Name(facing),
                BlueMonsterState.AttackTurnaround => Table.States["st_str" + StateNames.Name(facing.Opposite()) + "_rot"].Next,
                BlueMonsterState.Deciding or BlueMonsterState.DecidingAfterLeftTurn => Animation.State is null
                    ? prefix + "wlk" + StateNames.Name(facing)
                    : null,
                _ => null,
            };
        }

        /// <inheritdoc/>
        protected override bool IsFrozen(Entity entity)
        {
            return Body(entity).IsFrozen;
        }

        /// <inheritdoc/>
        protected override Direction Facing(Entity entity)
        {
            return Body(entity).FacingDirection;
        }

        private static BlueMonsterBody Body(Entity entity)
        {
            return entity.BlueMonsterBody ?? throw new ArgumentException("The entity is not a Blue Monster.", nameof(entity));
        }
    }
}
