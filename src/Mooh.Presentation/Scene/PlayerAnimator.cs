using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// Shows the recovered <c>CGameObjPlayer</c> state for what the simulation's player is doing.
    /// </summary>
    /// <remarks>
    /// <c>_OnCell05_Fly</c> is the player's own rule: a state plays its <c>FList</c> frames while
    /// the cell below holds nothing, re-checks at every clip end, and picks a random variant
    /// whenever the list changes.
    /// </remarks>
    /// <param name="library">The recovered animation tables.</param>
    /// <param name="random">The source of the original's random variant choice.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public sealed class PlayerAnimator(AnimationLibrary library, Random random) : EntityAnimator((library ?? throw new ArgumentNullException(nameof(library))).Table(AnimationLibrary.PlayerTable), random)
    {
        private const string FlyCheckHandler = "_OnCell05_Fly";

        /// <summary>Gets the pass: <c>ST_TPortMove</c> carries <c>RPass=2</c>, over everything else.</summary>
        public override int Pass => Animation.State == "st_portmove" ? 2 : 0;

        /// <inheritdoc/>
        public override IReadOnlyList<IReadOnlyList<string>> Candidates(string state, AnimationClip clip)
        {
            ArgumentNullException.ThrowIfNull(clip);
            return clip.FlyVariants is { Count: > 0 } fly && IsFlying() ? fly : clip.Variants;
        }

        /// <inheritdoc/>
        public override IReadOnlyList<string> PickVariant(IReadOnlyList<IReadOnlyList<string>> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            return candidates.Count == 0 ? [] : candidates[Random.Next(candidates.Count)];
        }

        /// <inheritdoc/>
        public override string? ResolveHandler(string handler)
        {
            return handler == FlyCheckHandler ? Animation.State : null;
        }

        /// <inheritdoc/>
        protected override string? DesiredState(WorldGrid world, Entity entity)
        {
            PlayerBody body = PlayerBody(entity);
            if (body.Reaction.Kind == PlayerReactionKind.Dead)
            {
                return "st_dead";
            }

            if (body.Reaction.Kind == PlayerReactionKind.Shocked)
            {
                return "st_shockin";
            }

            if (body.Action is PlayerAction action)
            {
                string direction = StateNames.Name(action.Direction);
                return action.Kind switch
                {
                    PlayerActionKind.StrikeWindup => "st_strin" + direction,
                    PlayerActionKind.StrikeRecovery => "st_strout" + direction,
                    PlayerActionKind.GrenadeThrow => "st_bomb" + direction,
                    PlayerActionKind.MinePlace => "st_mine" + direction,
                    PlayerActionKind.RocketLaunch => "st_rock" + direction,
                    PlayerActionKind.BigBombPlace => "st_bbombplace",
                    PlayerActionKind.ColaDrink => "st_boncola",
                    PlayerActionKind.TunnelEntry => "st_tonin" + direction,
                    PlayerActionKind.TunnelExit => "st_tonout" + direction,
                    PlayerActionKind.TeleportEntry => "st_tportin",
                    PlayerActionKind.TeleportFlight => "st_portmove",
                    PlayerActionKind.TeleportExit => "st_tportout",
                    PlayerActionKind.LevelEntry => "st_gstartinstart",
                    PlayerActionKind.ShelterIn => "st_dooroutstart",
                    PlayerActionKind.ShelterHidden => "st_doorouthide",
                    PlayerActionKind.ShelterOut => "st_doorinstart",
                    _ => Stand(body.FacingDirection),
                };
            }

            if (body.Rotation is PlayerRotation rotation)
            {
                return StateNames.Turn("st_", rotation.From, rotation.To);
            }

            if (body.TunnelTransit is PlayerTunnelTransit transit)
            {
                return "st_tonmove" + StateNames.Name(transit.Direction);
            }

            if (entity.Motion is { Cause: MotionCause.PlayerWalking } motion)
            {
                Direction travel = motion.TravelDirection;
                GridPosition ahead = entity.Position.Offset(travel);
                bool pushing = world.Contains(ahead)
                    && world.GetOccupants(ahead).Any(occupant => occupant.Motion?.Cause == MotionCause.PlayerPushed);
                return (pushing ? "st_wlkpush" : "st_wlk") + StateNames.Name(travel);
            }

            return Stand(body.FacingDirection);
        }

        /// <summary>
        /// Starts <c>ST_StPush</c>: held input into a cell the player cannot enter, from a
        /// standing state, plays the standing push before the player stands again.
        /// </summary>
        /// <inheritdoc/>
        protected override void React(IReadOnlyList<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            Direction facing = PlayerBody(Entity!).FacingDirection;
            if (Animation.State == Stand(facing)
                && events.Any(item => item is PlayerBlocked blocked && blocked.Direction == facing))
            {
                Animation.Play("st_stpush" + StateNames.Name(facing));
            }
        }

        private static PlayerBody PlayerBody(Entity entity)
        {
            return entity.PlayerBody ?? throw new ArgumentException("The entity is not the player.", nameof(entity));
        }

        private static string Stand(Direction direction)
        {
            return "st_st" + StateNames.Name(direction);
        }

        /// <summary>
        /// Models <c>_OnCell05_Fly</c>: the player flies while the cell below its drawn column holds
        /// no <c>CNT_CMaskCellLockMotion</c> occupant. Every cell-occupying object carries that mask;
        /// the shadow carriers the world list hides do not.
        /// </summary>
        private bool IsFlying()
        {
            if (World is null || Entity is null)
            {
                return false;
            }

            (double x, double y) = DrawnPosition.Of(Entity);
            GridPosition below = new((int)Math.Round(x), (int)Math.Round(y) + 1);
            return World.Contains(below)
                && !World.GetOccupants(below).Any(occupant => !occupant.Interactions.HasFlag(InteractionMask.OccupancyTransparent));
        }
    }
}
