using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Simulation;

namespace Mooh.Core.Player
{
    internal sealed class PlayerController(
        Entity player,
        WorldGrid world,
        MovementSystem movement,
        Direction gravityDirection,
        DifficultySpeeds difficultySpeeds,
        IPlayerActionEffects actionEffects)
    {
        private const double HalfTurnSeconds = 0.210;
        private const double QuarterTurnSeconds = 0.105;
        private const double CounterMotionMultiplier = 0.75;
        private const double TimeTolerance = 1e-12;

        /// <summary>The recovered <c>CNT_StrRTime</c>, the hand strike's own repeat time.</summary>
        private const double StrikeRepeatSeconds = 1.0;

        /// <summary>The recovered <c>RepTime</c> of <c>WeapGr</c>, the grenade.</summary>
        private const double GrenadeRepeatSeconds = 1.5;

        /// <summary>The recovered <c>RepTime</c> of the rocket, the mine and the big bomb.</summary>
        private const double OtherWeaponRepeatSeconds = 1.0;

        /// <summary>The recovered <c>ST_GStartInStart</c> plus <c>ST_GStartInInWalk</c>.</summary>
        private const double LevelEntrySeconds = 1.500 + 0.375;

        /// <summary>The recovered <c>ST_DoorOutStart</c> plus <c>ST_DoorOutOutWalk</c>.</summary>
        private const double ShelterInSeconds = 0.500 + 0.375;

        /// <summary>The recovered <c>ST_DoorOutHide</c>.</summary>
        private const double ShelterHiddenSeconds = 3.0;

        /// <summary>The recovered <c>ST_DoorInStart</c> plus <c>ST_DoorInInWalk</c>.</summary>
        private const double ShelterOutSeconds = 0.750 + 0.375;

        /// <summary>
        /// The recovered <c>ST_DoorCloseAndOut</c> plus <c>ST_DoorOut</c>, six frames at 75 ms each,
        /// after which an unused start door is killed.
        /// </summary>
        private const double StartDoorLeaveSeconds = 0.450 + 0.450;

        /// <summary>The recovered <c>ST_Rock*</c> duration: sixteen frames at 35 ms.</summary>
        private const double RocketLaunchSeconds = 0.560;

        /// <summary>The recovered <c>ST_Mine*</c> duration: ten frames at 25 ms.</summary>
        private const double MinePlaceSeconds = 0.250;

        /// <summary>The recovered <c>ST_BBombPlace</c> duration: fifteen frames at 50 ms.</summary>
        private const double BigBombPlaceSeconds = 0.750;

        /// <summary>The recovered <c>ST_Bomb*</c> duration: ten frames at 35 ms.</summary>
        private const double GrenadeThrowSeconds = 0.350;

        /// <summary>
        /// The recovered <c>ST_StrIn*</c> durations, indexed by <see cref="Direction"/>. Each state
        /// runs its animation at 15 ms per frame, and the frame counts differ per direction; the
        /// values are the on-ground <c>AList</c> counts, which <c>_OnCell05_Fly</c> selects
        /// whenever the cell below locks motion, as it does whenever the player is standing.
        /// </summary>
        private static readonly double[] StrikeWindupSeconds = [0.150, 0.135, 0.150, 0.105];

        /// <summary>The recovered <c>ST_StrOut*</c> recovery durations, indexed by direction.</summary>
        private static readonly double[] StrikeRecoverySeconds = [0.045, 0.060, 0.045, 0.075];

        /// <summary>
        /// The recovered <c>ST_TonIn*</c> and <c>ST_TonOut*</c> durations. Both run seven frames
        /// at 25 ms and neither moves the player, so entering and leaving a tunnel each cost the
        /// same fixed pause in every direction.
        /// </summary>
        private const double TunnelEntrySeconds = 0.175;

        /// <summary>The recovered <c>CNT_OPrm_SpeedTon</c> against the player's own speed.</summary>
        private const double TunnelSpeedMultiplier = 2.0;

        /// <summary>
        /// The recovered <c>ST_TPortIn</c> and <c>ST_TPortOut</c> durations. Both run twenty
        /// frames at 50 ms.
        /// </summary>
        private const double TeleportAnimationSeconds = 1.0;

        /// <summary>
        /// The recovered <c>CNT_TPortSp</c> of <c>0.0075 / 2.0</c> cells per millisecond, restated
        /// in cells per second.
        /// </summary>
        private const double TeleportFlightSpeed = 3.75;

        /// <summary>
        /// The recovered drink: <c>ST_BonCola</c> through <c>ST_BonCola03</c> run 6, 4, 5 and 8
        /// frames at 50 ms.
        /// </summary>
        private const double ColaDrinkSeconds = 1.150;

        private static readonly TimeSpan CellInteractionLock = TimeSpan.FromMilliseconds(150);

        private readonly IPlayerActionEffects _actionEffects = actionEffects
            ?? throw new ArgumentNullException(nameof(actionEffects));
        private readonly DifficultySpeeds _difficultySpeeds = difficultySpeeds
            ?? throw new ArgumentNullException(nameof(difficultySpeeds));
        private readonly MovementSystem _movement = movement
            ?? throw new ArgumentNullException(nameof(movement));
        private readonly Entity _player = player
            ?? throw new ArgumentNullException(nameof(player));
        private readonly WorldGrid _world = world
            ?? throw new ArgumentNullException(nameof(world));
        private Direction _gravityDirection = gravityDirection;
        private PlayerInputState _input;
        private Direction? _lastAppliedTravelDirection;
        private PlayerRotation? _pendingResume;

        public void SetInput(PlayerInputState input)
        {
            _input = input;
        }

        public void SetGravity(Direction direction)
        {
            _gravityDirection = direction;
        }

        /// <summary>
        /// Polls the held input for a stationary player, reproducing the recovered
        /// <c>GamePlayer.DoCtrl</c>: the fire branch is decided first and suppresses walking
        /// entirely, and only a player that is not firing runs <c>CtrlMove</c>.
        /// </summary>
        /// <param name="events">The ordered event sink.</param>
        /// <param name="fromCellArrival">
        /// Whether the poll follows a cell arrival. The original resolves an arrival through
        /// <c>_OnCell_Wlk</c>, which calls <c>CtrlMove</c> alone and never inspects the fire
        /// control, so a player walking with fire held keeps walking; the same path is the only
        /// one that reports a stop when nothing is held.
        /// </param>
        public void TryStart(
            List<SimulationEvent> events,
            bool fromCellArrival = false)
        {
            ArgumentNullException.ThrowIfNull(events);
            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            if (_player.Motion is not null
                || body.Rotation is not null
                || body.Action is not null
                || body.Reaction.Kind != PlayerReactionKind.Normal)
            {
                return;
            }

            if (_input.Fire && !fromCellArrival)
            {
                TryStartAction(body, events);
                return;
            }

            Direction? direction = _input.FirstHeldDirection();
            if (direction is not Direction heldDirection)
            {
                if (fromCellArrival)
                {
                    events.Add(new PlayerStopped(_player.Id));
                }

                return;
            }

            if (heldDirection != body.FacingDirection)
            {
                StartRotation(body, heldDirection, suspendedProgress: null, events);
                return;
            }

            StartMove(heldDirection, body, events);
        }

        /// <summary>Resumes a suspended reversal, or polls the held input.</summary>
        /// <param name="events">The ordered event sink.</param>
        /// <param name="fromCellArrival">Whether the poll follows a cell arrival.</param>
        public void ResumeOrTryStart(
            List<SimulationEvent> events,
            bool fromCellArrival)
        {
            if (_pendingResume is not PlayerRotation pendingResume)
            {
                TryStart(events, fromCellArrival);
                return;
            }

            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            if (_player.Motion is not null
                || body.Rotation is not null
                || body.Action is not null
                || body.Reaction.Kind != PlayerReactionKind.Normal)
            {
                return;
            }

            _pendingResume = null;
            GridPosition destination = _player.Position.Offset(pendingResume.To);
            _world.CommitMoves([new PlannedMove(_player.Id, _player.Position, destination)]);
            KinematicsSystem.Start(
                _player,
                MotionCause.PlayerWalking,
                pendingResume.To,
                body.MoveSpeed);
            _player.Motion!.Progress = 1 - pendingResume.SuspendedProgress!.Value;
            _lastAppliedTravelDirection = pendingResume.To;
            events.Add(new PlayerMoveStarted(_player.Id, pendingResume.To));
        }

        /// <summary>
        /// Decides the recovered fire branch. The weapon is used when the faced cell can hold it;
        /// otherwise the player strikes the cell by hand. Both branches share one repeat
        /// timestamp, and a branch that is still repeating leaves the player standing rather than
        /// falling back to walking, because the original returns without reaching <c>CtrlMove</c>.
        /// </summary>
        /// <remarks>
        /// One earlier step of the original branch is still unreachable: the healing shelter needs
        /// the door the player leaves at level start. The weapon branch is taken for the current
        /// weapon when the faced cell can hold it, and always for the big bomb, whose
        /// <c>bPlaceOnEmptyCell</c> is false; that weapon therefore never falls back to a strike.
        /// </remarks>
        private void TryStartAction(PlayerBody body, List<SimulationEvent> events)
        {
            if (TryEnterShelter(body, events) || TryUseTeleporter(body, events))
            {
                return;
            }

            Direction direction = body.FacingDirection;
            GridPosition target = _player.Position.Offset(direction);
            double now = _world.CurrentTimeSeconds;
            WeaponKind weapon = body.Progress.CurrentWeapon;
            if (TakesWeaponBranch(weapon, target))
            {
                if (IsRepeating(body, now, WeaponRepeatSeconds(weapon)))
                {
                    return;
                }

                body.LastActionSeconds = now;
                if (body.Progress.AmmoOf(weapon) <= 0)
                {
                    events.Add(new PlayerWeaponEmpty(_player.Id));
                    return;
                }

                StartWeapon(weapon, body, direction, target, events);
                return;
            }

            if (IsRepeating(body, now, StrikeRepeatSeconds))
            {
                return;
            }

            body.LastActionSeconds = now;
            double windup = StrikeWindupSeconds[(int)direction];
            body.Action = new PlayerAction(PlayerActionKind.StrikeWindup, direction, windup);
            events.Add(new PlayerStrikeStarted(
                _player.Id,
                direction,
                TimeSpan.FromSeconds(windup)));
        }

        /// <summary>
        /// Decides the recovered weapon-branch gate,
        /// <c>not x.bPlaceOnEmptyCell or not CellObjCheck(n, -1, CNT_OrMaskPlBombHere, True)</c>.
        /// </summary>
        private bool TakesWeaponBranch(WeaponKind weapon, GridPosition target)
        {
            return weapon == WeaponKind.BigBomb || CanPlaceWeapon(target);
        }

        /// <summary>
        /// Gets the recovered <c>RepTime</c> of a weapon: 1500 ms for the grenade and 1000 ms for
        /// the other three.
        /// </summary>
        private static double WeaponRepeatSeconds(WeaponKind weapon)
        {
            return weapon == WeaponKind.Grenade ? GrenadeRepeatSeconds : OtherWeaponRepeatSeconds;
        }

        /// <summary>Runs the current weapon's recovered <c>PlStart</c>.</summary>
        private void StartWeapon(
            WeaponKind weapon,
            PlayerBody body,
            Direction direction,
            GridPosition target,
            List<SimulationEvent> events)
        {
            switch (weapon)
            {
                case WeaponKind.Grenade:
                    StartGrenadeThrow(body, direction, target, events);
                    break;
                case WeaponKind.BigBomb:
                    StartBigBombPlace(body, events);
                    break;
                case WeaponKind.Mine:
                    _actionEffects.ClearWeaponCell(target, events);
                    StartWeaponUse(body, WeaponKind.Mine, PlayerActionKind.MinePlace, direction, MinePlaceSeconds, events);
                    break;
                case WeaponKind.Rocket:
                    // ST_RockStart has entries for left and right only, so Weap_Rocket does
                    // nothing facing up or down, although DoCtrl has spent the repeat time.
                    if (direction is Direction.Left or Direction.Right)
                    {
                        _actionEffects.ClearWeaponCell(target, events);
                        StartWeaponUse(body, WeaponKind.Rocket, PlayerActionKind.RocketLaunch, direction, RocketLaunchSeconds, events);
                    }

                    break;
                default:
                    throw new InvalidOperationException("Undefined weapon.");
            }
        }

        /// <summary>
        /// Applies the recovered <c>Weap_BBomb</c>, which places the bomb only when no other still,
        /// visible occupant shares the player's cell. Otherwise nothing happens, although
        /// <c>DoCtrl</c> has already spent the repeat time.
        /// </summary>
        private void StartBigBombPlace(PlayerBody body, List<SimulationEvent> events)
        {
            if (CellQuery.VisibleOccupants(_world, _player.Position).Any(
                occupant => occupant.Id != _player.Id && occupant.Motion is null))
            {
                return;
            }

            body.FacingDirection = Direction.Down;
            body.Action = new PlayerAction(PlayerActionKind.BigBombPlace, Direction.Down, BigBombPlaceSeconds);
            events.Add(new PlayerWeaponUseStarted(
                _player.Id,
                WeaponKind.BigBomb,
                Direction.Down,
                TimeSpan.FromSeconds(BigBombPlaceSeconds)));
        }

        private void StartWeaponUse(
            PlayerBody body,
            WeaponKind weapon,
            PlayerActionKind kind,
            Direction direction,
            double seconds,
            List<SimulationEvent> events)
        {
            body.Action = new PlayerAction(kind, direction, seconds);
            events.Add(new PlayerWeaponUseStarted(_player.Id, weapon, direction, TimeSpan.FromSeconds(seconds)));
        }

        /// <summary>Applies the recovered <c>Weap_Gas</c>.</summary>
        private void StartGrenadeThrow(
            PlayerBody body,
            Direction direction,
            GridPosition target,
            List<SimulationEvent> events)
        {
            _actionEffects.ClearWeaponCell(target, events);
            body.Action = new PlayerAction(
                PlayerActionKind.GrenadeThrow,
                direction,
                GrenadeThrowSeconds);
            events.Add(new PlayerGrenadeThrowStarted(
                _player.Id,
                direction,
                TimeSpan.FromSeconds(GrenadeThrowSeconds)));
        }

        /// <summary>
        /// Applies the recovered <c>_OnCell</c> priority for a recorded <c>StOnCell</c>: a cola
        /// collected earlier is drunk at this arrival instead of the walking handler running.
        /// </summary>
        /// <param name="events">The ordered event sink.</param>
        /// <returns><see langword="true"/> when the drink replaced the arrival handler.</returns>
        public bool TryStartPendingDrink(List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            if (!body.HasPendingColaDrink)
            {
                return false;
            }

            body.HasPendingColaDrink = false;

            // A hit on this same arrival has already entered the shock, and a shock abandons the
            // drink just as it does one already under way.
            if (body.Reaction.Kind != PlayerReactionKind.Normal)
            {
                return false;
            }

            body.Action = new PlayerAction(PlayerActionKind.ColaDrink, body.FacingDirection, ColaDrinkSeconds);
            events.Add(new PlayerColaDrinkStarted(_player.Id, TimeSpan.FromSeconds(ColaDrinkSeconds)));
            return true;
        }

        /// <summary>Spends one round and reports a resulting <c>WeapNext</c>.</summary>
        private void SpendAmmo(PlayerBody body, WeaponKind weapon, List<SimulationEvent> events)
        {
            if (body.Progress.SpendAmmo(weapon))
            {
                events.Add(new PlayerWeaponSelected(_player.Id, body.Progress.CurrentWeapon));
            }
        }

        /// <summary>
        /// Starts the recovered <c>ST_GStartInStart</c> emergence from the start door.
        /// </summary>
        public void BeginLevel()
        {
            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            body.FacingDirection = Direction.Down;
            body.Action = new PlayerAction(PlayerActionKind.LevelEntry, Direction.Down, LevelEntrySeconds);
        }

        /// <summary>
        /// Applies the first branch of the recovered <c>DoCtrl</c> fire control, <c>DoorGet</c>:
        /// with the start door in the player's own cell, the door opens and the player goes in.
        /// </summary>
        private bool TryEnterShelter(PlayerBody body, List<SimulationEvent> events)
        {
            Entity? door = _world.GetOccupants(_player.Position).FirstOrDefault(occupant => occupant.DoorBody is not null);
            if (door?.DoorBody is not DoorBody shelter)
            {
                return false;
            }

            shelter.IsShelter = true;
            shelter.RemovalRemainingSeconds = null;
            body.Action = new PlayerAction(PlayerActionKind.ShelterIn, body.FacingDirection, ShelterInSeconds);
            events.Add(new PlayerShelterEntered(
                _player.Id,
                TimeSpan.FromSeconds(ShelterInSeconds + ShelterHiddenSeconds + ShelterOutSeconds)));
            return true;
        }

        /// <summary>Gets the remaining duration of the active action, if any.</summary>
        public double? ActionSecondsRemaining => _player.PlayerBody?.Action?.RemainingSeconds;

        /// <summary>
        /// Gets whether the fire control is held by a player that is standing and controllable,
        /// which is exactly the state the recovered <c>DoCtrl</c> decides its fire branch in.
        /// </summary>
        public bool IsAwaitingAction => _input.Fire
            && _player.Motion is null
            && _player.PlayerBody is
            { Rotation: null, Action: null, Reaction.Kind: PlayerReactionKind.Normal };

        /// <summary>
        /// Gets how long a held fire control still has to wait for its repeat time, or
        /// <see langword="null"/> when nothing is waiting. The advance loop uses it as a timer, so
        /// a repeat that expires part way through an advance fires exactly when it expires.
        /// </summary>
        public double? SecondsUntilActionReady
        {
            get
            {
                if (!IsAwaitingAction
                    || _player.PlayerBody is not { LastActionSeconds: double last } body)
                {
                    return null;
                }

                WeaponKind weapon = body.Progress.CurrentWeapon;
                double repeat = TakesWeaponBranch(weapon, _player.Position.Offset(body.FacingDirection))
                    ? WeaponRepeatSeconds(weapon)
                    : StrikeRepeatSeconds;
                double remaining = last + repeat - _world.CurrentTimeSeconds;
                return remaining > TimeTolerance ? remaining : null;
            }
        }

        /// <summary>Advances the active action and applies each phase as it ends.</summary>
        /// <param name="elapsedSeconds">The elapsed simulation time.</param>
        /// <param name="events">The ordered event sink.</param>
        /// <returns>Whether the player became controllable again.</returns>
        public bool AdvanceAction(double elapsedSeconds, List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            if (body.Action is not PlayerAction action)
            {
                return false;
            }

            action.RemainingSeconds -= elapsedSeconds;
            if (action.Kind == PlayerActionKind.TeleportFlight
                && body.Teleport is PlayerTeleport flight
                && flight.TotalSeconds > 0)
            {
                flight.Progress = Math.Clamp(
                    1 - (action.RemainingSeconds / flight.TotalSeconds),
                    0,
                    1);
            }

            if (action.RemainingSeconds > TimeTolerance)
            {
                return false;
            }

            GridPosition target = _player.Position.Offset(action.Direction);
            switch (action.Kind)
            {
                case PlayerActionKind.StrikeWindup:
                    body.Action = new PlayerAction(
                        PlayerActionKind.StrikeRecovery,
                        action.Direction,
                        StrikeRecoverySeconds[(int)action.Direction]);
                    ApplyStrike(action.Direction, target, events);
                    return false;
                case PlayerActionKind.GrenadeThrow:
                    body.Action = null;
                    if (_world.Contains(target))
                    {
                        _actionEffects.PlaceGrenade(target, events);
                        SpendAmmo(body, WeaponKind.Grenade, events);
                    }
                    return true;
                case PlayerActionKind.StrikeRecovery:
                    body.Action = null;
                    return true;
                case PlayerActionKind.TunnelEntry:
                    body.Action = null;
                    StartTunnelLeg(body.TunnelTransit!.Direction, body);
                    return false;
                case PlayerActionKind.TunnelExit:
                    body.Action = null;
                    body.FacingDirection = action.Direction;
                    return true;
                case PlayerActionKind.TeleportEntry:
                    StartTeleportFlight(body, events);
                    return false;
                case PlayerActionKind.TeleportFlight:
                    CompleteTeleportFlight(body, events);
                    return false;
                case PlayerActionKind.TeleportExit:
                    body.Action = null;
                    body.Teleport = null;
                    body.FacingDirection = Direction.Down;
                    return true;
                case PlayerActionKind.LevelEntry:
                    // The recovered _AEnd_GStartCloseAndStay starts the door's close-and-out,
                    // clears its mask and leaves the player standing, facing down.
                    body.Action = null;
                    body.FacingDirection = Direction.Down;
                    foreach (Entity door in _world.GetOccupants(_player.Position).Where(occupant => occupant.DoorBody is not null))
                    {
                        door.Interactions = InteractionMask.None;
                        door.DoorBody!.RemovalRemainingSeconds = StartDoorLeaveSeconds;
                    }

                    return true;
                case PlayerActionKind.ShelterIn:
                    body.Action = new PlayerAction(PlayerActionKind.ShelterHidden, action.Direction, ShelterHiddenSeconds);
                    return false;
                case PlayerActionKind.ShelterHidden:
                    _player.HealthBody?.RestoreFull();
                    body.Action = new PlayerAction(PlayerActionKind.ShelterOut, action.Direction, ShelterOutSeconds);
                    return false;
                case PlayerActionKind.ShelterOut:
                    body.Action = null;
                    body.FacingDirection = Direction.Down;
                    return true;
                case PlayerActionKind.RocketLaunch:
                    body.Action = null;
                    if (_world.Contains(target))
                    {
                        _actionEffects.FireRocket(target, action.Direction, events);
                        SpendAmmo(body, WeaponKind.Rocket, events);
                    }

                    return true;
                case PlayerActionKind.MinePlace:
                    body.Action = null;
                    if (_world.Contains(target))
                    {
                        _actionEffects.LayMine(target, events);
                        SpendAmmo(body, WeaponKind.Mine, events);
                    }

                    return true;
                case PlayerActionKind.BigBombPlace:
                    // The recovered _AEnd_BBomb places the bomb, enters ST_StBottom and spends it.
                    body.Action = null;
                    body.FacingDirection = Direction.Down;
                    _actionEffects.PlaceBigBomb(_player.Position, events);
                    SpendAmmo(body, WeaponKind.BigBomb, events);
                    return true;
                case PlayerActionKind.ColaDrink:
                    // The recovered _AEnd_BonCola turns the bonus on and enters ST_StBottom.
                    body.Action = null;
                    body.FacingDirection = Direction.Down;
                    body.Progress.StartCola();
                    return true;
                default:
                    throw new InvalidOperationException("Unknown player action.");
            }
        }

        /// <summary>
        /// Applies the recovered <c>_AEnd_Str</c>. The struck cell is consumed first, exactly as
        /// walking into it would consume it, and every remaining occupant that carries
        /// <c>CNT_OrMaskPlPush</c> is then pushed. An occupant already moving against the strike
        /// fails the <c>CellObjStillOrDir</c> gate and nothing is pushed at all.
        /// </summary>
        /// <remarks>
        /// The recovered loop offers each occupant <c>PlCtrl</c> before it offers <c>OnPlPush</c>
        /// and takes only the first that answers, so an occupant with a <c>PlCtrl</c> handler is
        /// never pushed. Among placed types the arrow is the only one that is both pushable and
        /// defines the handler: striking an arrow launches it instead of shoving it. A rotator
        /// answers with its windup, and a frozen monster with the <c>_Ice_Ctrl</c> shatter that
        /// <c>IceOn</c> installs. This is also
        /// the only path that reaches an arrow's <c>PlCtrl</c> at all, because <c>DoCtrl</c>
        /// dispatches only to occupants of the player's own cell with a negative <c>RPass</c>,
        /// and the arrow locks its cell and passes at zero.
        /// </remarks>
        private void ApplyStrike(
            Direction direction,
            GridPosition target,
            List<SimulationEvent> events)
        {
            if (!_world.Contains(target))
            {
                return;
            }

            events.Add(new PlayerStruck(_player.Id, target, direction));
            EatDestination(target, events);
            IReadOnlyList<Entity> occupants = _world.GetOccupants(target);
            if (occupants.Any(
                occupant => occupant.Motion is not null
                    && occupant.Motion.TravelDirection != direction))
            {
                return;
            }

            foreach (Entity occupant in occupants)
            {
                if (occupant.FlyingArrowBody is not null)
                {
                    _actionEffects.LaunchArrow(occupant, events);
                    continue;
                }

                if (occupant.WorldRotationBody is not null)
                {
                    _actionEffects.TriggerWorldRotation(occupant, events);
                    continue;
                }

                if (occupant.IsFrozenMonster)
                {
                    _actionEffects.ShatterFrozenMonster(occupant, events);
                    continue;
                }

                if ((occupant.Interactions
                    & (InteractionMask.PlayerPushable | InteractionMask.PushesThroughOccupants)) != 0)
                {
                    _actionEffects.PushStruck(occupant, direction, events);
                }
            }
        }

        /// <summary>
        /// Determines whether the faced cell can receive a placed weapon, applying the recovered
        /// <c>CellObjCheck(n, -1, CNT_OrMaskPlBombHere, True)</c>. A still occupant blocks the
        /// weapon unless it is crushable, which <c>Game._CellPlKill</c> then destroys, or a shadow
        /// object. <c>CNT_CMaskShadowObj</c> has no flag of its own, because every carrier of it —
        /// the table, the teleporter, fire and the spawned beetle — is exactly an occupant that
        /// does not lock its cell.
        /// </summary>
        private bool CanPlaceWeapon(GridPosition target)
        {
            return _world.Contains(target)
                && !_world.GetOccupants(target).Any(
                    occupant => occupant.Motion is null
                        && occupant.Interactions.HasFlag(InteractionMask.BlocksMovement)
                        && !occupant.Interactions.HasFlag(InteractionMask.CanBePushedThrough));
        }

        private static bool IsRepeating(PlayerBody body, double now, double repeatSeconds)
        {
            return body.LastActionSeconds is double last
                && now - last < repeatSeconds - TimeTolerance;
        }

        private void StartMove(
            Direction direction,
            PlayerBody body,
            List<SimulationEvent> events)
        {

            GridPosition destination = _player.Position.Offset(direction);
            if (TryEnterTunnel(destination, direction, body, events))
            {
                return;
            }

            if (_world.Contains(destination))
            {
                KeyDoorSystem.TryOpen(_player, _world, destination, events);
            }

            EatDestination(destination, events);
            MovementPlan plan = _movement.Plan(
                new MoveRequest(_player.Id, direction),
                _gravityDirection);
            foreach (EntityId offered in plan.PushOffered)
            {
                _actionEffects.OfferPush(_world.GetEntity(offered), events);
            }

            if (plan.Result == MoveResult.Blocked)
            {
                // ST_StPush plays 16 frames sideways and 10 vertically, at 35 ms, and is not
                // bDoAmb: the idle ambient waits until it has played out.
                body.IdleSeconds = -(direction is Direction.Left or Direction.Right ? 0.56 : 0.35);
                body.AmbientRemainingSeconds = 0;
                events.Add(new PlayerBlocked(_player.Id, direction));
                return;
            }

            bool hasCounterMotion = plan.Result == MoveResult.Pushed
                ? CellQuery.HasCounterMotion(_world, destination, direction)
                    || CellQuery.HasCounterMotion(_world, destination.Offset(direction), direction)
                : CellQuery.HasCounterMotion(_world, destination, direction);
            double speedMultiplier = hasCounterMotion ? CounterMotionMultiplier : 1;

            if (plan.Result == MoveResult.Pushed)
            {
                RemovePushThroughOccupants(destination.Offset(direction), events);
            }

            _world.CommitMoves(
                [.. plan.Steps.Where(step => step.From != step.To)
                    .Select(step => new PlannedMove(step.EntityId, step.From, step.To))]);

            if (plan.Result == MoveResult.Pushed)
            {
                StartPushedMembers(plan, direction, body, speedMultiplier, events);
            }

            KinematicsSystem.Start(
                _player,
                MotionCause.PlayerWalking,
                direction,
                body.MoveSpeed * speedMultiplier);
            _lastAppliedTravelDirection = direction;
            events.Add(new PlayerMoveStarted(_player.Id, direction));
        }

        private void StartPushedMembers(
            MovementPlan plan,
            Direction direction,
            PlayerBody playerBody,
            double speedMultiplier,
            List<SimulationEvent> events)
        {
            int resolutionOrder = 0;
            foreach (MovementStep step in plan.Steps)
            {
                if (step.EntityId == _player.Id)
                {
                    continue;
                }

                Entity pushed = _world.GetEntity(step.EntityId);
                double progress = pushed.Motion?.Progress ?? 0;
                KinematicsSystem.Stop(pushed);
                KinematicsSystem.Start(
                    pushed,
                    MotionCause.PlayerPushed,
                    direction,
                    playerBody.MoveSpeed * playerBody.PushSpeedMultiplier * speedMultiplier,
                    resolutionOrder);
                pushed.Motion!.Progress = progress;
                resolutionOrder++;
                events.Add(new PushStarted(pushed.Id, direction));
            }
        }

        public void HandleInTransit(List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            MotionState? motion = _player.Motion;
            if (motion is null || motion.Cause != MotionCause.PlayerWalking)
            {
                return;
            }

            Direction? requestedDirection = FirstHeldAlong(motion.SegmentDirection);
            if (requestedDirection is not Direction direction
                || direction == _lastAppliedTravelDirection)
            {
                return;
            }

            if (direction == motion.TravelDirection)
            {
                _lastAppliedTravelDirection = direction;
                return;
            }

            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            KinematicsSystem.Stop(_player);
            _lastAppliedTravelDirection = null;
            StartRotation(body, direction, motion.Progress, events);
        }

        public double? RotationSecondsRemaining => _player.PlayerBody?.Rotation?.RemainingSeconds;

        public bool AdvanceRotation(double elapsedSeconds, List<SimulationEvent> events)
        {
            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            PlayerRotation? rotation = body.Rotation;
            if (rotation is null)
            {
                return false;
            }

            rotation.RemainingSeconds -= elapsedSeconds;
            if (rotation.RemainingSeconds > TimeTolerance)
            {
                return false;
            }

            body.Rotation = null;
            events.Add(new PlayerRotationCompleted(_player.Id, rotation.To));
            if (rotation.SuspendedProgress is not null)
            {
                _pendingResume = rotation;
            }

            return true;
        }

        private void StartRotation(
            PlayerBody body,
            Direction direction,
            double? suspendedProgress,
            List<SimulationEvent> events)
        {
            Direction from = body.FacingDirection;
            double duration = direction == from.Opposite()
                ? HalfTurnSeconds
                : QuarterTurnSeconds;
            body.FacingDirection = direction;
            body.Rotation = new PlayerRotation(from, direction, duration, suspendedProgress);
            events.Add(new PlayerRotationStarted(_player.Id, from, direction));
        }

        public void EndSegment()
        {
            _lastAppliedTravelDirection = null;
            _pendingResume = null;
        }

        /// <summary>
        /// Applies the recovered <c>PlCtrl</c> dispatch <c>DoCtrl</c> runs before it reaches any
        /// weapon: every occupant of the player's own cell with a negative <c>RPass</c> is offered
        /// the control, and the first that answers consumes it. The teleporter is the only placed
        /// type that qualifies. Unlike the weapon and strike branches this one never touches
        /// <c>ulWeapTime</c>, because the original returns before the repeat check.
        /// </summary>
        /// <returns><see langword="true"/> when a teleporter took the control.</returns>
        private bool TryUseTeleporter(PlayerBody body, List<SimulationEvent> events)
        {
            Entity? teleporter = _world.GetOccupants(_player.Position)
                .FirstOrDefault(occupant => occupant.TeleporterBody is not null);
            if (teleporter?.TeleporterBody is not TeleporterBody links)
            {
                return false;
            }

            GridPosition destination = links.TakeNextDestination(teleporter.Position);
            double distance = Math.Sqrt(
                Math.Pow(destination.X - _player.Position.X, 2)
                + Math.Pow(destination.Y - _player.Position.Y, 2));
            body.Teleport = new PlayerTeleport(
                _player.Position,
                destination,
                distance / TeleportFlightSpeed);
            body.Action = new PlayerAction(
                PlayerActionKind.TeleportEntry,
                body.FacingDirection,
                TeleportAnimationSeconds);
            ApplyShadow(true);
            events.Add(new PlayerTeleportStarted(
                _player.Id,
                _player.Position,
                destination,
                TimeSpan.FromSeconds(TeleportAnimationSeconds)));
            return true;
        }

        /// <summary>
        /// Enters the recovered <c>ST_TPortMove</c>. Its length is the straight-line distance
        /// divided by <c>CNT_TPortSp</c>, which is the continuous limit of the original's
        /// per-cycle shift, and a teleporter linked to its own cell therefore takes no time at
        /// all while still costing both animations.
        /// </summary>
        private void StartTeleportFlight(PlayerBody body, List<SimulationEvent> events)
        {
            PlayerTeleport teleport = body.Teleport
                ?? throw new InvalidOperationException("The player is not teleporting.");
            if (teleport.TotalSeconds <= TimeTolerance)
            {
                CompleteTeleportFlight(body, events);
                return;
            }

            body.Action = new PlayerAction(
                PlayerActionKind.TeleportFlight,
                body.FacingDirection,
                teleport.TotalSeconds);
        }

        /// <summary>
        /// Lands the recovered <c>_TPortCFunc</c> arrival. The destination cell is consumed, the
        /// player is snapped into it, and a cell that still holds a visible occupant is blown open
        /// by <c>Game._ObjExplWrd</c> — which is what keeps a teleporter linked into solid rock
        /// from burying the player.
        /// </summary>
        private void CompleteTeleportFlight(PlayerBody body, List<SimulationEvent> events)
        {
            PlayerTeleport teleport = body.Teleport
                ?? throw new InvalidOperationException("The player is not teleporting.");
            teleport.Progress = 1;
            GridPosition destination = _world.Contains(teleport.Destination)
                ? teleport.Destination
                : _player.Position;
            EatDestination(destination, events);
            _world.Relocate(_player.Id, destination);
            if (CellQuery.VisibleOccupants(_world, destination).Any(
                occupant => occupant.Id != _player.Id))
            {
                _actionEffects.ExplodeCell(destination, _player.Id, events);
            }

            ApplyShadow(false);
            body.Action = new PlayerAction(
                PlayerActionKind.TeleportExit,
                body.FacingDirection,
                TeleportAnimationSeconds);
            events.Add(new PlayerTeleportArrived(_player.Id, destination));
        }

        /// <summary>
        /// Applies the recovered <c>PlComeHere</c> gate. <c>CtrlMove</c> offers the destination
        /// cell's occupants the direction of travel before anything else — before the push gate
        /// and before the cell is eaten — and returns outright as soon as one accepts. A tunnel
        /// is the only placed type that answers, and it accepts when its <c>EnDirMap</c> has an
        /// opening on the side the player is arriving from.
        /// </summary>
        /// <returns><see langword="true"/> when a tunnel took over the move.</returns>
        private bool TryEnterTunnel(
            GridPosition destination,
            Direction direction,
            PlayerBody body,
            List<SimulationEvent> events)
        {
            if (!_world.Contains(destination)
                || !_world.GetOccupants(destination).Any(
                    occupant => occupant.TunnelBody?.Shape.Route(direction) is not null))
            {
                return false;
            }

            body.TunnelTransit = new PlayerTunnelTransit(direction);
            body.FacingDirection = direction;
            body.Action = new PlayerAction(
                PlayerActionKind.TunnelEntry,
                direction,
                TunnelEntrySeconds);
            ApplyShadow(true);
            events.Add(new PlayerEnteredTunnel(
                _player.Id,
                destination,
                direction,
                TimeSpan.FromSeconds(TunnelEntrySeconds)));
            return true;
        }

        /// <summary>
        /// Starts one leg of tunnel travel, the recovered <c>ST_TonMove*</c>. The player commits
        /// the destination cell exactly as any other move does and travels at
        /// <c>CNT_OPrm_SpeedTon</c> times its own speed.
        /// </summary>
        private void StartTunnelLeg(Direction direction, PlayerBody body)
        {
            GridPosition destination = _player.Position.Offset(direction);
            _world.CommitMoves([new PlannedMove(_player.Id, _player.Position, destination)]);
            KinematicsSystem.Start(
                _player,
                MotionCause.TunnelTransit,
                direction,
                body.MoveSpeed * TunnelSpeedMultiplier);
        }

        /// <summary>
        /// Applies the recovered <c>_TonMoveOnCell</c> to a player that has just arrived in a
        /// cell while travelling. The tunnel in the arrival cell decides where to go next; a cell
        /// without one ends the journey.
        /// </summary>
        /// <param name="events">The ordered event sink.</param>
        public void ResolveTunnelArrival(List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            PlayerTunnelTransit transit = body.TunnelTransit
                ?? throw new InvalidOperationException("The player is not travelling through a tunnel.");
            KinematicsSystem.Stop(_player);
            _lastAppliedTravelDirection = null;

            TunnelBody? tunnel = _world.GetOccupants(_player.Position)
                .Select(occupant => occupant.TunnelBody)
                .FirstOrDefault(candidate => candidate is not null);
            if (tunnel is null)
            {
                body.TunnelTransit = null;
                ApplyShadow(false);
                body.Action = new PlayerAction(
                    PlayerActionKind.TunnelExit,
                    transit.Direction,
                    TunnelEntrySeconds);
                events.Add(new PlayerLeftTunnel(
                    _player.Id,
                    _player.Position,
                    transit.Direction,
                    TimeSpan.FromSeconds(TunnelEntrySeconds)));
                return;
            }

            // A route of null is the recovered DIR_Last, which a connected tunnel run never
            // produces. Treating it as a blocked exit keeps a malformed layout from walking the
            // player out of the world instead of failing loudly on an undefined direction.
            Direction? route = tunnel.Shape.Route(transit.Direction, transit.HasReversed);
            if (route is Direction exit && TryContinue(exit, transit, events))
            {
                StartTunnelLeg(transit.Direction, body);
                return;
            }

            transit.HasReversed = true;
            transit.Direction = transit.Direction.Opposite();
            events.Add(new PlayerTunnelReversed(_player.Id, _player.Position, transit.Direction));
            StartTunnelLeg(transit.Direction, body);
        }

        /// <summary>
        /// Decides whether travel may continue into the cell beyond, applying the recovered pair
        /// of tests. The cell is free when it holds no non-shadow occupant lacking
        /// <c>CNT_CMaskPlCanEat</c>, in which case its edible contents are consumed on the way
        /// through; otherwise travel continues only when the blocking cell is itself a tunnel
        /// with an opening on that side.
        /// </summary>
        private bool TryContinue(
            Direction exit,
            PlayerTunnelTransit transit,
            List<SimulationEvent> events)
        {
            GridPosition next = _player.Position.Offset(exit);
            if (!_world.Contains(next))
            {
                return false;
            }

            bool blocked = CellQuery.VisibleOccupants(_world, next).Any(
                occupant => !occupant.Interactions.HasFlag(InteractionMask.PlayerEdible));
            if (!blocked)
            {
                EatDestination(next, events);
                transit.Direction = exit;
                return true;
            }

            bool continues = _world.GetOccupants(next).Any(
                occupant => occupant.TunnelBody?.Shape.Route(exit, transit.HasReversed) is not null);
            if (!continues)
            {
                return false;
            }

            transit.Direction = exit;
            return true;
        }

        /// <summary>
        /// Adds or removes the recovered <c>bShadow</c> pair, which hides the player from monster
        /// navigation and from every ordinary cell-occupancy query alike. Every tunnel state
        /// except <c>ST_TonOut*</c> carries it, and <c>TPortMe</c> sets the same two bits by hand
        /// for the teleport, which <c>ST_TPortOut</c> then clears by not carrying them.
        /// </summary>
        private void ApplyShadow(bool shadowed)
        {
            const InteractionMask shadow = InteractionMask.MonsterTransparent
                | InteractionMask.OccupancyTransparent;
            _player.Interactions = shadowed
                ? _player.Interactions | shadow
                : _player.Interactions & ~shadow;
        }

        private void EatDestination(
            GridPosition destination,
            List<SimulationEvent> events)
        {
            if (!_world.Contains(destination))
            {
                return;
            }

            _world.LockCell(destination, CellInteractionLock);
            foreach (Entity entity in _world.GetOccupants(destination))
            {
                if (!entity.Interactions.HasFlag(InteractionMask.PlayerEdible)
                    || entity.IsKillLocked(_world.CurrentTimeSeconds))
                {
                    continue;
                }

                PickupSystem.Collect(_player, entity, _world, _difficultySpeeds, events);
                _ = _world.Remove(entity.Id);
                events.Add(new PlayerAte(_player.Id, entity.Id));
            }
        }

        /// <summary>
        /// Applies the recovered crush a walking push runs on the cell beyond, which is the same
        /// <c>CellObjDump(n, CNT_CMaskCallCanPushTh)</c> over <c>Game.ObjKill</c> that a placed
        /// weapon runs. It is routed through the simulation coordinator so a crushed object leaves
        /// the world by the one kill path, exactly as the engine's single killed-object flag does.
        /// </summary>
        private void RemovePushThroughOccupants(GridPosition destination, List<SimulationEvent> events)
        {
            if (_world.GetOccupants(destination).Count == 0)
            {
                return;
            }

            _world.LockCell(destination, CellInteractionLock);
            _actionEffects.CrushCell(destination, events);
        }

        private Direction? FirstHeldAlong(Direction segmentDirection)
        {
            Direction opposite = segmentDirection.Opposite();
            return _input.Left && IsAlong(Direction.Left) ? Direction.Left
                : _input.Up && IsAlong(Direction.Up) ? Direction.Up
                : _input.Right && IsAlong(Direction.Right) ? Direction.Right
                : _input.Down && IsAlong(Direction.Down) ? Direction.Down
                : null;

            bool IsAlong(Direction direction)
            {
                return direction == segmentDirection || direction == opposite;
            }
        }
    }
}
