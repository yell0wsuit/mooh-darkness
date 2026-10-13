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
    /// The simulation owns every gameplay transition, so each update derives the state the
    /// original would be in and enters it when that changes. Chains the original runs from its
    /// animation table alone — the shock, the death, the door waits — play on by themselves.
    /// <c>_OnCell05_Fly</c> is the one rule of the player's own: a state plays its <c>FList</c>
    /// frames while the cell below holds nothing, and re-checks at every clip end.
    /// </remarks>
    public sealed class PlayerAnimator : IAnimationRules
    {
        private const string FlyCheckHandler = "_OnCell05_Fly";
        private readonly Random _random;
        private string? _desired;
        private Entity? _player;
        private WorldGrid? _world;

        /// <summary>Initializes the animator.</summary>
        /// <param name="library">The recovered animation tables.</param>
        /// <param name="random">The source of the original's random variant choice.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public PlayerAnimator(AnimationLibrary library, Random random)
        {
            ArgumentNullException.ThrowIfNull(library);
            ArgumentNullException.ThrowIfNull(random);
            _random = random;
            Animation = new AnimationPlayer(library.Table(AnimationLibrary.PlayerTable), this);
        }

        /// <summary>Gets the player's animation.</summary>
        public AnimationPlayer Animation
        {
            get;
        }

        /// <summary>Advances the animation by a frame of simulation, then follows the player.</summary>
        /// <param name="world">The simulated world.</param>
        /// <param name="player">The player entity.</param>
        /// <param name="events">The events the simulation reported for this frame.</param>
        /// <param name="seconds">The simulated time of this frame.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="player"/> has no player body.</exception>
        public void Update(WorldGrid world, Entity player, IReadOnlyList<SimulationEvent> events, double seconds)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(events);
            PlayerBody body = player.PlayerBody
                ?? throw new ArgumentException("The entity is not the player.", nameof(player));
            _world = world;
            _player = player;

            Animation.Advance(seconds);
            string desired = DesiredState(world, player, body);
            if (desired != _desired || Animation.State is null)
            {
                _desired = desired;
                Animation.Play(desired);
            }

            // ST_StPush: held input into a cell the player cannot enter, from a standing state.
            if (Animation.State == desired
                && desired == Stand(body.FacingDirection)
                && events.Any(item => item is PlayerBlocked blocked && blocked.Direction == body.FacingDirection))
            {
                Animation.Play("st_stpush" + Name(body.FacingDirection));
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<IReadOnlyList<string>> Candidates(string state, AnimationClip clip)
        {
            ArgumentNullException.ThrowIfNull(clip);
            return clip.FlyVariants is { Count: > 0 } fly && IsFlying() ? fly : clip.Variants;
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> PickVariant(IReadOnlyList<IReadOnlyList<string>> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            return candidates.Count == 0 ? [] : candidates[_random.Next(candidates.Count)];
        }

        /// <inheritdoc/>
        public double FrameMilliseconds(AnimationClip clip)
        {
            ArgumentNullException.ThrowIfNull(clip);
            return clip.FrameMilliseconds[0];
        }

        /// <inheritdoc/>
        public string? ResolveHandler(string handler)
        {
            return handler == FlyCheckHandler ? Animation.State : null;
        }

        private static string DesiredState(WorldGrid world, Entity player, PlayerBody body)
        {
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
                string direction = Name(action.Direction);
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
                return $"st_rot{Initial(rotation.From)}2{Initial(rotation.To)}";
            }

            if (body.TunnelTransit is PlayerTunnelTransit transit)
            {
                return "st_tonmove" + Name(transit.Direction);
            }

            if (player.Motion is { Cause: MotionCause.PlayerWalking } motion)
            {
                Direction travel = motion.TravelDirection;
                bool pushing = world.Contains(player.Position.Offset(travel))
                    && world.GetOccupants(player.Position.Offset(travel))
                        .Any(occupant => occupant.Motion?.Cause == MotionCause.PlayerPushed);
                return (pushing ? "st_wlkpush" : "st_wlk") + Name(travel);
            }

            return Stand(body.FacingDirection);
        }

        private static string Stand(Direction direction)
        {
            return "st_st" + Name(direction);
        }

        /// <summary>Gets the recovered state suffix of a direction, such as <c>bottom</c>.</summary>
        private static string Name(Direction direction)
        {
            return direction switch
            {
                Direction.Left => "left",
                Direction.Up => "top",
                Direction.Right => "right",
                Direction.Down => "bottom",
                _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
        }

        /// <summary>Gets the recovered turn-state letter of a direction, such as <c>b</c>.</summary>
        private static char Initial(Direction direction)
        {
            return Name(direction)[0];
        }

        /// <summary>
        /// Models <c>_OnCell05_Fly</c>: the player flies while the cell below its drawn column holds
        /// no <c>CNT_CMaskCellLockMotion</c> occupant. Every cell-occupying object carries that mask;
        /// the shadow carriers the world list hides do not.
        /// </summary>
        private bool IsFlying()
        {
            if (_world is null || _player is null)
            {
                return false;
            }

            (double x, double y) = DrawnPosition.Of(_player);
            GridPosition below = new((int)Math.Round(x), (int)Math.Round(y) + 1);
            return _world.Contains(below)
                && !_world.GetOccupants(below).Any(occupant => !occupant.Interactions.HasFlag(InteractionMask.OccupancyTransparent));
        }
    }
}
