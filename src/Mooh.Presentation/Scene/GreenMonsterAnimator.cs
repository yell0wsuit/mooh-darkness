using System;

using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>Shows the recovered <c>CGameObjGrMon</c> state for a simulated Green Monster.</summary>
    /// <remarks>
    /// The strike is <c>ST_Str</c> (375 ms) into <c>ST_Str0</c> (250 ms), whose end strikes; the
    /// recovery is <c>ST_Str1</c> (500 ms), which ends in the clockwise turn the simulation starts
    /// next.
    /// </remarks>
    /// <param name="library">The recovered animation tables.</param>
    /// <param name="random">The source of the original's random phase.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public sealed class GreenMonsterAnimator(AnimationLibrary library, Random random) : MonsterAnimator((library ?? throw new ArgumentNullException(nameof(library))).Table(AnimationLibrary.GreenMonsterTable), random, IceFrames)
    {
        /// <summary>The recovered <c>IceAnims</c>: one vertical frame serves both up and down.</summary>
        private static readonly string[] IceFrames = ["OIceLeft", "OIceVert", "OIceRight", "OIceVert"];

        /// <inheritdoc/>
        protected override string? MonsterState(Entity entity)
        {
            GreenMonsterBody body = Body(entity);
            Direction facing = body.FacingDirection;
            string prefix = body.IsSmall ? "st_sm" : "st_";
            return body.State switch
            {
                GreenMonsterState.TransformingSmall or GreenMonsterState.TransformingNormal => "st_boneye" + StateNames.Name(facing),
                GreenMonsterState.Walking => prefix + "wlk" + StateNames.Name(entity.Motion?.TravelDirection ?? facing),
                GreenMonsterState.Turning => StateNames.Turn(prefix, facing, facing.TurnRight()),
                GreenMonsterState.AttackWindup => "st_str" + StateNames.Name(facing),
                GreenMonsterState.AttackRecovery => "st_str1" + StateNames.Name(facing),
                GreenMonsterState.Deciding => Animation.State is null ? prefix + "wlk" + StateNames.Name(facing) : null,
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

        private static GreenMonsterBody Body(Entity entity)
        {
            return entity.GreenMonsterBody ?? throw new ArgumentException("The entity is not a Green Monster.", nameof(entity));
        }
    }
}
