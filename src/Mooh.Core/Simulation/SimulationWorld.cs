using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Player;
using Mooh.Core.Shakes;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Coordinates continuous gravity evaluation and kinematic advancement for a world.
    /// </summary>
    public sealed class SimulationWorld : IPlayerActionEffects
    {
        private const double BoundaryTolerance = 1e-6;
        private const double ExplosionDelaySeconds = 0.2;

        /// <summary>The recovered <c>CNT_GPrmExplCrossDelay</c> before a cross blast's arms start.</summary>
        private const double CrossBlastDelaySeconds = 0.15;

        /// <summary>The recovered <c>CNT_GPrmExplCrossLen</c>, the cells each cross-blast arm can reach.</summary>
        private const int CrossBlastArmLength = 3;

        /// <summary>
        /// The recovered <c>CNT_WTime</c> of <c>_DoMagic</c>: each cell of the 3x3 is worked after
        /// its Chebyshev distance from the gem times this delay.
        /// </summary>
        private const double MagicRingDelaySeconds = 0.2;

        /// <summary>The recovered <c>CNT_BBombEn</c>, the energy a big bomb's flood starts with.</summary>
        private const int BigBombEnergy = 15;

        /// <summary>The recovered <c>CNT_BBombEnKill</c>, the energy a cell with an explodable costs.</summary>
        private const int BigBombHitCost = 14;

        /// <summary>The recovered <c>CNT_BBombChDelay</c> between one flood ring and the next.</summary>
        private const double BigBombStepSeconds = 0.2;

        /// <summary>The recovered <c>CNT_BioSpawnProb</c>, compared with a 15-bit draw by <c>Rand.Prob</c>.</summary>
        private const int BioSpawnProbability = 2048 * 3 / 2;

        /// <summary>The recovered <c>CNT_BioSpawnSp</c> against the normal speed.</summary>
        private const double BioSpreadSpeedMultiplier = 4.0;

        /// <summary>The recovered <c>ST_SpawnVert</c>/<c>ST_SpawnHor</c> length: 10 frames at 50 ms.</summary>
        private const double BioSpawnSeconds = 0.5;

        /// <summary>The recovered <c>ST_Grow</c> length: 20 frames at 50 ms.</summary>
        private const double BioGrowSeconds = 1.0;

        /// <summary>The number of frames in the recovered bio-mass <c>ST_Stay</c> loop.</summary>
        private const int BioStayFrames = 20;

        /// <summary>The recovered <c>CNT_MagicKillLockTime</c> a spawned diamond cannot be killed for.</summary>
        private const double MagicKillLockSeconds = 0.5;

        /// <summary>The recovered <c>CNT_ONameMagicSpawn</c> types a magic gem picks from.</summary>
        private static readonly string[] MagicSpawnTypes = ["ODiam01", "ODiam02", "ODiam03", "ODiam04", "ODiam05"];
        private const int MonsterKillScore = 10;
        private const int BeetleAbsorbScore = 90;

        /// <summary>
        /// The placed type whose recovered behavior a thrown grenade shares. The original registers
        /// it separately as <c>WBombInt</c> and gives it the same <c>GameTypeBomb</c> behavior the
        /// placed <c>WBomb</c> pickup has.
        /// </summary>
        private const string ThrownGrenadeType = "WBomb";

        /// <summary>
        /// The placed type a laid mine shares. The original's <c>GameObjMineMake</c> makes
        /// <c>CNT_ONameMine</c>, the same <c>GameObjMine</c> type the editor places.
        /// </summary>
        private const string MineType = "WMine";

        /// <summary>The placed type a spread bio mass shares, the recovered <c>CNT_ONameBio</c>.</summary>
        private const string BioMassType = "MMBio";

        /// <summary>
        /// The recovered <c>CNT_OPrm_SpeedSmBeetle</c> factor against <c>Compl.SpeedMon</c>. Each
        /// segment is further scaled by <c>Rand.FRange(0.9, 1.1)</c> from the seeded source.
        /// </summary>
        private const double BeetleSpeedMultiplier = 0.5;
        private const double TimeTolerance = 1e-12;

        /// <summary>The recovered <c>CNT_OPrm_PlPushSpeed</c> against the player's own speed.</summary>
        private const double PushSpeedMultiplier = 1.5;

        /// <summary>
        /// The recovered <c>ST_Work</c> windup an arrow runs before it starts flying: seven
        /// frames at 35 ms. It is the same in all four directions.
        /// </summary>
        private const double ArrowWindupSeconds = 0.245;

        /// <summary>
        /// The recovered <c>CNT_OPrmRocketSpeed</c> against <c>Compl.SpeedNrm</c>, shared by the
        /// flying arrow and the fired rocket.
        /// </summary>
        private const double ArrowFlightSpeedMultiplier = 2.0;

        /// <summary>
        /// The recovered <c>ST_Work</c> windup a struck rotator runs before the world starts
        /// turning: twenty frames at 50 ms, the same in both rotator types. Ordinary play carries
        /// on for its whole duration; only the turn itself freezes the simulation.
        /// </summary>
        private const double WorldRotationWindupSeconds = 1.0;

        /// <summary>
        /// The recovered spin duration. <c>CNT_GPrm_WorldRotSp</c> is 0.002, and the native
        /// <c>WorldRotStart</c> (<c>sub_4A09D0</c>) divides it by <c>pi/2</c> before accumulating
        /// it per millisecond until the total reaches one, so a quarter turn takes
        /// <c>pi/2 / 0.002</c> milliseconds.
        /// </summary>
        private const double WorldRotationSpinSeconds = Math.PI / 2 / 0.002 / 1000;

        /// <summary>The recovered <c>CNT_OPrm_AirSlow</c> applied against a counter-moving cell.</summary>
        private const double CounterMotionMultiplier = 0.75;

        /// <summary>
        /// Gets how long a cell a blast has reached stays locked. This is also how long the cell
        /// is dangerous, so a presentation layer can use it as the lifetime of a blast effect.
        /// </summary>
        public static TimeSpan ExplosionCellLockDuration { get; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// The recovered <c>CNT_WrdExplLockTime</c> that <c>Game._CellPlKill</c> applies to a cell
        /// a weapon is placed in.
        /// </summary>
        private static readonly TimeSpan WeaponCellLockDuration = TimeSpan.FromMilliseconds(500);
        private static readonly (int X, int Y)[] ExplosionNeighborOffsets =
        [
            (-1, 0),
            (1, 0),
            (-1, -1),
            (0, -1),
            (1, -1),
            (-1, 1),
            (0, 1),
            (1, 1),
        ];
        private static readonly TimeSpan TimerTolerance = TimeSpan.FromTicks(1);

        private readonly BlueMonsterSystem _blueMonsters;
        private readonly DifficultySpeeds _difficultySpeeds;
        private readonly GravitySystem _gravity;
        private readonly GreenMonsterSystem _greenMonsters;
        private readonly LevelOutcomeSystem _levelOutcome;
        private readonly double _maximumTravelPerSubstep;
        private readonly MovementSystem _movement;
        private readonly List<ScheduledExplosionCell> _pendingExplosionCells = [];
        private readonly PlayerController? _playerController;
        private readonly Random _random;
        private readonly SmallBeetleSystem _smallBeetles;
        private readonly WorldGrid _world;
        private Direction _gravityDirection;
        private long _nextExplosionOrder;
        private long _nextSpawnOrder;
        private WorldRotationDirection _rotationDirection;
        private EntityId? _rotationOwner;
        private double _rotationRemainingSeconds;

        /// <summary>Initializes a simulation coordinator.</summary>
        /// <param name="world">The authoritative world.</param>
        /// <param name="gravityDirection">The direction in which unsupported bodies fall.</param>
        /// <param name="collisionPolicy">The gravity collision policy.</param>
        /// <param name="maximumTravelPerSubstep">The maximum cell fraction traveled in one substep.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="maximumTravelPerSubstep"/> is not finite or lies outside (0, 1].
        /// </exception>
        public SimulationWorld(
            WorldGrid world,
            Direction gravityDirection,
            IGravityCollisionPolicy collisionPolicy,
            double maximumTravelPerSubstep = 0.5)
            : this(
                world,
                gravityDirection,
                collisionPolicy,
                Random.Shared,
                maximumTravelPerSubstep,
                DifficultySpeeds.Standard)
        {
        }

        /// <summary>Initializes a simulation coordinator with a supplied random source.</summary>
        /// <param name="world">The authoritative world.</param>
        /// <param name="gravityDirection">The direction in which unsupported bodies fall.</param>
        /// <param name="collisionPolicy">The gravity collision policy.</param>
        /// <param name="random">The random generator used to select equivalent slide routes.</param>
        /// <param name="maximumTravelPerSubstep">The maximum cell fraction traveled in one substep.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="maximumTravelPerSubstep"/> is not finite or lies outside (0, 1].
        /// </exception>
        public SimulationWorld(
            WorldGrid world,
            Direction gravityDirection,
            IGravityCollisionPolicy collisionPolicy,
            Random random,
            double maximumTravelPerSubstep = 0.5)
            : this(
                world,
                gravityDirection,
                collisionPolicy,
                random,
                maximumTravelPerSubstep,
                DifficultySpeeds.Standard)
        {
        }

        /// <summary>Initializes a simulation coordinator with explicit random and difficulty sources.</summary>
        /// <param name="world">The authoritative world.</param>
        /// <param name="gravityDirection">The direction in which unsupported bodies fall.</param>
        /// <param name="collisionPolicy">The gravity collision policy.</param>
        /// <param name="random">The random generator used to select equivalent slide routes.</param>
        /// <param name="maximumTravelPerSubstep">The maximum cell fraction traveled in one substep.</param>
        /// <param name="difficultySpeeds">The active recovered gameplay difficulty values.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="maximumTravelPerSubstep"/> is not finite or lies outside (0, 1].
        /// </exception>
        public SimulationWorld(
            WorldGrid world,
            Direction gravityDirection,
            IGravityCollisionPolicy collisionPolicy,
            Random random,
            double maximumTravelPerSubstep,
            DifficultySpeeds difficultySpeeds)
        {
            if (!double.IsFinite(maximumTravelPerSubstep)
                || maximumTravelPerSubstep <= 0
                || maximumTravelPerSubstep > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTravelPerSubstep));
            }

            _world = world ?? throw new ArgumentNullException(nameof(world));
            _difficultySpeeds = difficultySpeeds ?? throw new ArgumentNullException(nameof(difficultySpeeds));
            _gravityDirection = gravityDirection;
            _maximumTravelPerSubstep = maximumTravelPerSubstep;
            _gravity = new GravitySystem(collisionPolicy, random);
            _random = random;
            _movement = new MovementSystem(world, new DefaultInteractionPolicy());
            _blueMonsters = new BlueMonsterSystem(world);
            _greenMonsters = new GreenMonsterSystem(world);
            _smallBeetles = new SmallBeetleSystem(world, random);
            _levelOutcome = new LevelOutcomeSystem(world, _difficultySpeeds);

            // The recovered GameInit counts the treasure the player can reach.
            if (world.Player is { PlayerBody: PlayerBody startingBody } startingPlayer)
            {
                startingBody.Progress.StartTreasureTrace(
                    TreasureTrace.CountReachable(world, startingPlayer.Position));
            }

            StartBioMasses();

            // The recovered GameInit starts PlayTime on the timed profile only.
            if (_difficultySpeeds.IsTimed && world.Player?.HealthBody is HealthBody playerHealth)
            {
                playerHealth.Clock = new PlayClock();
            }

            _playerController = world.Player is Entity player
                ? new PlayerController(player, world, _movement, gravityDirection, _difficultySpeeds, this)
                : null;
        }

        /// <summary>
        /// Gets whether the level has been decided. The outcome becomes observable only after the
        /// recovered settle following the deciding condition.
        /// </summary>
        public LevelOutcome Outcome => _levelOutcome.Outcome;

        /// <summary>
        /// Gets the direction of the quarter turn currently in flight, or <see langword="null"/>
        /// when the world is not turning. Nothing else in the simulation advances while it is set,
        /// so a caller that drives its own clock sees elapsed time absorbed by the turn.
        /// </summary>
        public WorldRotationDirection? WorldRotationInFlight => _rotationOwner is null
            ? null
            : _rotationDirection;

        /// <summary>
        /// Gets how far the quarter turn in flight has come, from zero to one. It is zero when the
        /// world is not turning. The turn itself is atomic, so this exists for presentation: it is
        /// the recovered rotation accumulator a renderer scales to an angle.
        /// </summary>
        public double WorldRotationProgress => _rotationOwner is null
            ? 0
            : 1 - (_rotationRemainingSeconds / WorldRotationSpinSeconds);

        /// <summary>Replaces the currently held input for the world's player.</summary>
        /// <param name="input">The latest held cardinal inputs.</param>
        /// <exception cref="InvalidOperationException">The world has no player.</exception>
        public void SetPlayerInput(PlayerInputState input)
        {
            PlayerController controller = _playerController
                ?? throw new InvalidOperationException("Player input requires a world with a player.");
            controller.SetInput(input);
        }

        /// <summary>
        /// Starts the level the way the recovered <c>GamePlayer.GameInit</c> does: a door appears in
        /// the player's cell and the player emerges from it.
        /// </summary>
        /// <returns>The ordered events of the level start.</returns>
        public IReadOnlyList<SimulationEvent> BeginLevel()
        {
            if (_world.Player is not Entity player || _playerController is null)
            {
                return [];
            }

            // The recovered door mask is ShadowObj | MonShadow, and GameInit adds CellLockMotion
            // until the player is out.
            Entity door = new(
                CreateSpawnedEntityId(player.Id),
                player.Position,
                InteractionMask.BlocksMovement
                    | InteractionMask.OccupancyTransparent
                    | InteractionMask.MonsterTransparent,
                doorBody: new DoorBody());
            _world.Add(door);
            _playerController.BeginLevel();
            return [];
        }

        private double? SecondsUntilNextDoorLeaves()
        {
            double? result = null;
            foreach (Entity entity in _world.Entities)
            {
                if ((entity.DoorBody?.RemovalRemainingSeconds ?? entity.KeyDoorBody?.OpeningRemainingSeconds) is double remaining
                    && (result is null || remaining < result))
                {
                    result = remaining;
                }
            }

            return result;
        }

        /// <summary>
        /// Plays out every leaving start door and kills each one that has gone, then every opening
        /// key door.
        /// </summary>
        private void AdvanceDoors(double elapsedSeconds, List<SimulationEvent> events)
        {
            Entity[] leaving = [.. _world.Entities.Where(entity => entity.DoorBody?.RemovalRemainingSeconds is not null)];
            foreach (Entity door in leaving)
            {
                DoorBody body = door.DoorBody!;
                double remaining = body.RemovalRemainingSeconds!.Value - elapsedSeconds;
                if (remaining > TimeTolerance)
                {
                    body.RemovalRemainingSeconds = remaining;
                    continue;
                }

                body.RemovalRemainingSeconds = null;
                DestroyEntity(door, door.Id, events);
            }

            AdvanceKeyDoors(elapsedSeconds, events);
        }

        /// <summary>
        /// Plays out every opening key door. <c>KeyDoorKill</c> kills the door at the end of its
        /// animation, and the kill is what scores it.
        /// </summary>
        private void AdvanceKeyDoors(double elapsedSeconds, List<SimulationEvent> events)
        {
            Entity[] opening = [.. _world.Entities.Where(entity => entity.KeyDoorBody?.OpeningRemainingSeconds is not null)];
            foreach (Entity door in opening)
            {
                KeyDoorBody body = door.KeyDoorBody!;
                double remaining = body.OpeningRemainingSeconds!.Value - elapsedSeconds;
                if (remaining > TimeTolerance)
                {
                    body.OpeningRemainingSeconds = remaining;
                    continue;
                }

                body.OpeningRemainingSeconds = null;
                _ = _world.Remove(door.Id);
                if (_world.Player is Entity player && player.PlayerBody is PlayerBody playerBody)
                {
                    int score = playerBody.Progress.OpenDoor();
                    events.Add(new KeyDoorOpened(player.Id, door.Id, door.ArchivalIdentity?.Variant ?? 0, score));
                }
            }
        }

        /// <summary>Selects a weapon, the recovered <c>WeapCurrSet</c> the <c>1</c>-<c>4</c> keys call.</summary>
        /// <param name="weapon">The weapon to make current.</param>
        /// <returns>The selection event, or nothing when the weapon is current or empty.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="weapon"/> is undefined.</exception>
        public IReadOnlyList<SimulationEvent> SelectWeapon(WeaponKind weapon)
        {
            return !Enum.IsDefined(weapon)
                ? throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null)
                : _world.Player is { PlayerBody.Progress: PlayerProgress progress } player
                && progress.SelectWeapon(weapon)
                ? [new PlayerWeaponSelected(player.Id, weapon)]
                : [];
        }

        /// <summary>
        /// Selects the next stocked weapon, the recovered <c>WeapNext</c> the second fire control
        /// runs when it is released.
        /// </summary>
        /// <returns>The selection event, or nothing when no other weapon is stocked.</returns>
        public IReadOnlyList<SimulationEvent> SelectNextWeapon()
        {
            return _world.Player is { PlayerBody.Progress: PlayerProgress progress } player
                && progress.SelectNextWeapon()
                ? [new PlayerWeaponSelected(player.Id, progress.CurrentWeapon)]
                : [];
        }

        /// <summary>Applies health damage and starts any corresponding player reaction.</summary>
        /// <param name="request">The requested damage.</param>
        /// <returns>The complete ordered event sequence produced by the request.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="KeyNotFoundException">The target entity is not registered.</exception>
        public IReadOnlyList<SimulationEvent> ApplyDamage(DamageRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            List<SimulationEvent> events = [];
            ApplyDamage(request, events);
            return events;
        }

        /// <summary>Applies or removes the recovered glasses effect for every Green Monster.</summary>
        /// <param name="active">Whether monsters should use their small, player-edible form.</param>
        /// <returns>The ordered transformation events produced by the change.</returns>
        public IReadOnlyList<SimulationEvent> SetGreenMonsterGlasses(bool active)
        {
            List<SimulationEvent> events = [];
            if (active && _world.Player?.PlayerBody?.Progress is PlayerProgress progress)
            {
                progress.ClearIce();
                SynchronizeMonsterIce(events);
            }

            foreach (Entity monster in _world.Entities.Where(entity => entity.GreenMonsterBody is not null))
            {
                _greenMonsters.SetSmall(monster, active, events);
            }

            return events;
        }

        /// <summary>
        /// Starts a registered explosive's own recovered blast: the delayed 3x3 explosion, or the
        /// barrel's cross blast.
        /// </summary>
        /// <param name="sourceEntityId">The entity that becomes the explosion center.</param>
        /// <returns>The immediate ordered events; later cells remain scheduled.</returns>
        /// <exception cref="KeyNotFoundException">The source is not registered.</exception>
        /// <exception cref="InvalidOperationException">The source does not explode on its own.</exception>
        public IReadOnlyList<SimulationEvent> Detonate(EntityId sourceEntityId)
        {
            Entity source = _world.GetEntity(sourceEntityId);
            if (source.ExplosionBody?.Reaction is not (ExplosionReaction.Detonate3x3
                or ExplosionReaction.CrossBlast
                or ExplosionReaction.BigBomb))
            {
                throw new InvalidOperationException("Detonation requires an entity with its own blast.");
            }

            List<SimulationEvent> events = [];
            StartOwnBlast(source, events);

            _gravity.EvaluateStationary(_world, _gravityDirection, events);
            return events;
        }

        private void ApplyDamage(DamageRequest request, List<SimulationEvent> events)
        {
            Entity target = _world.GetEntity(request.TargetEntityId);
            DamageResult result = DamageSystem.Apply(target, request, events);
            if (result != DamageResult.Applied || target.PlayerBody is not PlayerBody body)
            {
                return;
            }

            KinematicsSystem.Stop(target);
            body.Rotation = null;
            body.Action = null;
            _playerController?.EndSegment();
            if (target.HealthBody!.IsDead)
            {
                MarkPlayerDead(target, body, events);
            }
            else
            {
                body.Reaction.Kind = PlayerReactionKind.Shocked;
                body.Reaction.Remaining = body.ShockDuration;
                events.Add(new PlayerShockStarted(target.Id, body.ShockDuration));
            }
        }

        private static void MarkPlayerDead(Entity player, PlayerBody body, List<SimulationEvent> events)
        {
            body.Reaction.Kind = PlayerReactionKind.Dead;
            body.Reaction.Remaining = TimeSpan.Zero;
            events.Add(new PlayerDied(player.Id));
        }

        /// <summary>
        /// Applies the recovered <c>Game.ModCycle</c> drain, which runs outside timed play while
        /// the player is not in a <c>bImmort</c> state. The timed profile's rate is zero.
        /// </summary>
        private bool IsDraining => _difficultySpeeds.HealthDrainPerSecond > 0
            && _world.Player is { PlayerBody.IsImmortal: false, HealthBody.IsDead: false };

        /// <summary>
        /// Gets how long the drain needs to empty the bar. It is a substep bound so the loss
        /// branch observes the exact zero crossing, and so an otherwise idle advance still drains.
        /// </summary>
        private double? SecondsUntilDrainEmpties()
        {
            return IsDraining
                ? _world.Player!.HealthBody!.Current / _difficultySpeeds.HealthDrainPerSecond
                : null;
        }

        /// <summary>
        /// Gets how long the timed clock has left. It is a substep bound so the timeout branch
        /// observes the exact moment the clock runs out.
        /// </summary>
        private double? SecondsUntilClockRunsOut()
        {
            return _world.Player?.HealthBody?.Clock is { HasRunOut: false } clock
                ? clock.Remaining.TotalSeconds
                : null;
        }

        private void DrainPlayerHealth(double elapsedSeconds)
        {
            if (!IsDraining)
            {
                return;
            }

            // The substep that reaches the zero crossing is cut to it exactly, so a floating
            // remainder is snapped rather than left to schedule vanishing substeps.
            HealthBody health = _world.Player!.HealthBody!;
            double amount = elapsedSeconds * _difficultySpeeds.HealthDrainPerSecond;
            health.Drain(health.Current - amount < TimeTolerance ? health.Current : amount);
        }

        /// <summary>
        /// Applies the <c>PObj.StateSet(ST_Dead)</c> of the recovered loss branch. A lethal hit has
        /// already put the player there; health emptied by the drain reaches it only here.
        /// </summary>
        private void ObserveLossDeath(List<SimulationEvent> events)
        {
            if (_levelOutcome.IsSettlingLoss
                && _world.Player is Entity player
                && player.PlayerBody is PlayerBody body
                && body.Reaction.Kind != PlayerReactionKind.Dead)
            {
                KinematicsSystem.Stop(player);
                body.Rotation = null;
                body.Action = null;
                _playerController?.EndSegment();
                MarkPlayerDead(player, body, events);
            }
        }

        /// <summary>Changes gravity direction while all entities are stationary.</summary>
        /// <param name="direction">The new cardinal gravity direction.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="direction"/> is not a defined <see cref="Direction"/> value.
        /// </exception>
        /// <exception cref="InvalidOperationException">An entity is currently moving.</exception>
        public void SetGravity(Direction direction)
        {
            if (!Enum.IsDefined(direction))
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }

            if (_world.Entities.Any(entity => entity.Motion is not null))
            {
                throw new InvalidOperationException("Gravity cannot change while an entity is moving.");
            }

            _gravityDirection = direction;
            _playerController?.SetGravity(direction);
        }

        /// <summary>Advances gravity-driven motion by the specified elapsed time.</summary>
        /// <param name="elapsed">The non-negative elapsed simulation time.</param>
        /// <returns>The ordered events produced during the update.</returns>
        public IReadOnlyList<SimulationEvent> Advance(TimeSpan elapsed)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

            if (elapsed == TimeSpan.Zero)
            {
                return [];
            }

            ValidateMotionState();
            StartBioMasses();
            List<SimulationEvent> events = [];
            SynchronizeMonsterIce(events);
            _gravity.EvaluateStationary(_world, _gravityDirection, events);
            DecideMonsters(events);
            _playerController?.TryStart(events);
            ResolveWeaponCollisions(events);
            _levelOutcome.Observe();

            double remainingSeconds = elapsed.TotalSeconds;
            while (remainingSeconds > 0)
            {
                // A turn in flight replaces everything else, which is the recovered
                // Game.ModCycle:2693 swapping the whole object cycle for WorldRotCycle. Not even
                // the world clock advances, so cell locks and repeat times keep their remainders.
                if (_rotationOwner is not null)
                {
                    double spinSeconds = Math.Min(remainingSeconds, _rotationRemainingSeconds);
                    _rotationRemainingSeconds -= spinSeconds;
                    remainingSeconds -= spinSeconds;
                    if (remainingSeconds < TimeTolerance)
                    {
                        remainingSeconds = 0;
                    }

                    if (_rotationRemainingSeconds <= TimeTolerance)
                    {
                        CompleteWorldRotation(events);
                    }

                    continue;
                }

                _playerController?.HandleInTransit(events);
                Entity[] moving = [.. _world.Entities.Where(entity => entity.Motion is not null)];
                double? rotationSeconds = _playerController?.RotationSecondsRemaining;
                double? actionSeconds = _playerController?.ActionSecondsRemaining
                    ?? _playerController?.SecondsUntilActionReady;
                double? unlockSeconds = _world.SecondsUntilNextCellUnlock();
                double? healthSeconds = SecondsUntilNextImmunityExpiry();
                double? bonusSeconds = SecondsUntilNextBonusExpiry();
                double? reactionSeconds = SecondsUntilPlayerReactionEnds();
                double? monsterSeconds = SecondsUntilNextMonsterTimer();
                double? explosionSeconds = SecondsUntilNextExplosionCell();
                double? beetleSeconds = _smallBeetles.SecondsUntilNextTimer();
                double? outcomeSeconds = _levelOutcome.SecondsUntilSettle();
                double? arrowSeconds = SecondsUntilNextArrowWindup();
                double? rotatorSeconds = SecondsUntilNextRotatorWindup();
                double? balloonSeconds = SecondsUntilNextBalloonDetonation();
                double? drainSeconds = SecondsUntilDrainEmpties();
                double? clockSeconds = SecondsUntilClockRunsOut();
                double? bigBombSeconds = SecondsUntilNextBigBomb();
                double? bioSeconds = SecondsUntilNextBioMass();
                double? doorSeconds = SecondsUntilNextDoorLeaves();

                double substepSeconds = remainingSeconds;
                if (moving.Length != 0)
                {
                    double maximumSpeed = moving.Max(entity => entity.Motion!.Speed);
                    double boundarySeconds = moving.Min(
                        entity => entity.Motion!.TravelSign > 0
                            ? (1 - entity.Motion.Progress) / entity.Motion.Speed
                            : entity.Motion.Progress / entity.Motion.Speed);
                    substepSeconds = Math.Min(
                        substepSeconds,
                        Math.Min(_maximumTravelPerSubstep / maximumSpeed, boundarySeconds));
                }

                if (rotationSeconds is double remainingRotation)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingRotation);
                }

                if (actionSeconds is double remainingAction)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingAction);
                }

                if (unlockSeconds is double remainingUntilUnlock)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingUntilUnlock);
                }

                if (healthSeconds is double remainingImmunity)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingImmunity);
                }

                if (bonusSeconds is double remainingBonus)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingBonus);
                }

                if (reactionSeconds is double remainingReaction)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingReaction);
                }

                if (monsterSeconds is double remainingMonsterState)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingMonsterState);
                }

                if (explosionSeconds is double remainingExplosionDelay)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingExplosionDelay);
                }

                if (beetleSeconds is double remainingBeetleTimer)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingBeetleTimer);
                }

                if (outcomeSeconds is double remainingOutcomeSettle)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingOutcomeSettle);
                }

                if (arrowSeconds is double remainingArrowWindup)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingArrowWindup);
                }

                if (rotatorSeconds is double remainingRotatorWindup)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingRotatorWindup);
                }

                if (balloonSeconds is double remainingFuse)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingFuse);
                }

                if (drainSeconds is double remainingHealth)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingHealth);
                }

                if (clockSeconds is double remainingPlayTime)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingPlayTime);
                }

                if (bigBombSeconds is double remainingBigBomb)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingBigBomb);
                }

                if (bioSeconds is double remainingBio)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingBio);
                }

                if (doorSeconds is double remainingDoor)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingDoor);
                }

                if (moving.Length == 0
                    && rotationSeconds is null
                    && actionSeconds is null
                    && unlockSeconds is null
                    && healthSeconds is null
                    && bonusSeconds is null
                    && reactionSeconds is null
                    && monsterSeconds is null
                    && explosionSeconds is null
                    && beetleSeconds is null
                    && outcomeSeconds is null
                    && arrowSeconds is null
                    && rotatorSeconds is null
                    && balloonSeconds is null
                    && drainSeconds is null
                    && clockSeconds is null
                    && bigBombSeconds is null
                    && bioSeconds is null
                    && doorSeconds is null)
                {
                    _world.AdvanceTime(remainingSeconds);
                    break;
                }

                foreach (Entity entity in moving)
                {
                    KinematicsSystem.Advance(entity, substepSeconds);
                }

                _world.AdvanceTime(substepSeconds);
                AdvanceHealthImmunity(substepSeconds);
                AdvancePlayerProgress(substepSeconds);
                DrainPlayerHealth(substepSeconds);

                // Fuses burn before anything this substep can light or place a new one, so a fuse
                // started part way through is never charged for time that passed before it.
                AdvanceBalloonFuses(substepSeconds, events);
                AdvanceBigBombFuses(substepSeconds, events);
                AdvanceBioMasses(substepSeconds, events);
                AdvanceDoors(substepSeconds, events);
                _world.Player?.HealthBody?.Clock?.Advance(SimulationTime.Elapsed(substepSeconds));
                SynchronizeMonsterIce(events);
                bool playerControlReady = _playerController?.AdvanceRotation(
                    substepSeconds,
                    events) == true;
                playerControlReady |= _playerController?.AdvanceAction(substepSeconds, events) == true;
                playerControlReady |= AdvancePlayerReaction(substepSeconds, events);
                // Beetles advance first, so a wait that expires this substep is decided by the
                // DecideMonsters pass at the end of AdvanceMonsterTimers rather than one substep
                // later.
                AdvanceBeetles(substepSeconds, events);
                AdvanceMonsterTimers(substepSeconds, events);
                AdvanceArrowWindups(substepSeconds, events);
                AdvanceRotatorWindups(substepSeconds, events);
                bool resolvedExplosion = ResolvePendingExplosionCells(events);

                remainingSeconds -= substepSeconds;
                if (remainingSeconds < TimeTolerance)
                {
                    remainingSeconds = 0;
                }

                bool resolvedBoundary = ResolveBoundaries(
                    events,
                    out bool boundaryControlReady,
                    out bool fromCellArrival);
                playerControlReady |= boundaryControlReady;
                if (!resolvedBoundary
                    && unlockSeconds is double expiringLock
                    && substepSeconds >= expiringLock - TimeTolerance)
                {
                    _gravity.EvaluateStationary(_world, _gravityDirection, events);
                }
                else if (resolvedExplosion)
                {
                    _gravity.EvaluateStationary(_world, _gravityDirection, events);
                }

                // The original runs DoCtrl once per physics substep, so a repeat time that expires
                // part way through an advance fires on the next substep instead of waiting for the
                // next advance. Polling a standing player that holds the fire control reproduces
                // that without changing any path the control is not held on.
                playerControlReady |= _playerController?.IsAwaitingAction == true;
                if (playerControlReady)
                {
                    _playerController?.ResumeOrTryStart(events, fromCellArrival);
                }

                ResolveWeaponCollisions(events);

                _levelOutcome.Advance(substepSeconds, events);
                ObserveLossDeath(events);
            }

            return events;
        }

        private double? SecondsUntilNextBonusExpiry()
        {
            PlayerProgress? progress = _world.Player?.PlayerBody?.Progress;
            if (progress is null)
            {
                return null;
            }

            TimeSpan remaining = new[] { progress.ColaRemaining, progress.IceRemaining }
                .Where(value => value > TimeSpan.Zero)
                .Order()
                .FirstOrDefault();
            return remaining > TimeSpan.Zero ? remaining.TotalSeconds : null;
        }

        private void AdvancePlayerProgress(double elapsedSeconds)
        {
            _world.Player?.PlayerBody?.Progress.Advance(SimulationTime.Elapsed(elapsedSeconds));
        }

        /// <summary>
        /// Applies the recovered <c>Game.BonIceWork</c>, which walks both monster families with one
        /// implementation and turns each one's ice on or off with the bonus.
        /// </summary>
        private void SynchronizeMonsterIce(List<SimulationEvent> events)
        {
            bool iceActive = _world.Player?.PlayerBody?.Progress.IceRemaining > TimeSpan.Zero;
            foreach (Entity monster in _world.Entities.Where(IsMonster))
            {
                if (monster.GreenMonsterBody is GreenMonsterBody green)
                {
                    if (!iceActive)
                    {
                        GreenMonsterSystem.Thaw(monster, events);
                        continue;
                    }

                    if (green.IsSmall)
                    {
                        _greenMonsters.SetSmall(monster, isSmall: false, events);
                    }

                    _greenMonsters.Freeze(monster, events);
                }
                else if (iceActive)
                {
                    _blueMonsters.Freeze(monster, events);
                }
                else
                {
                    BlueMonsterSystem.Thaw(monster, events);
                }
            }
        }

        private double? SecondsUntilNextImmunityExpiry()
        {
            TimeSpan? remaining = _world.Entities
                .Select(entity => entity.HealthBody?.ImmunityRemaining)
                .Where(value => value > TimeSpan.Zero)
                .Min();
            return remaining?.TotalSeconds;
        }

        private double? SecondsUntilNextMonsterTimer()
        {
            double? green = _greenMonsters.SecondsUntilNextTimer();
            double? blue = _blueMonsters.SecondsUntilNextTimer();
            return green is null ? blue : blue is null ? green : Math.Min(green.Value, blue.Value);
        }

        private double? SecondsUntilNextExplosionCell()
        {
            if (_pendingExplosionCells.Count == 0)
            {
                return null;
            }

            double dueTime = _pendingExplosionCells.Min(value => value.DueTimeSeconds);
            return Math.Max(0, dueTime - _world.CurrentTimeSeconds);
        }

        private bool ResolvePendingExplosionCells(List<SimulationEvent> events)
        {
            ScheduledExplosionCell[] ready =
            [
                .. _pendingExplosionCells
                    .Where(value => value.DueTimeSeconds <= _world.CurrentTimeSeconds + TimeTolerance)
                    .OrderBy(value => value.Order),
            ];
            if (ready.Length == 0)
            {
                return false;
            }

            foreach (ScheduledExplosionCell scheduled in ready)
            {
                _ = _pendingExplosionCells.Remove(scheduled);
                switch (scheduled.Kind)
                {
                    case ScheduledCellKind.Magic:
                        WorkMagicCell(scheduled.SourceEntityId, scheduled.Position, events);
                        break;
                    case ScheduledCellKind.BigBomb:
                        WorkBigBombCell(
                            scheduled.SourceEntityId,
                            scheduled.Position,
                            scheduled.Remaining,
                            scheduled.Flood!,
                            events);
                        break;
                    case ScheduledCellKind.CrossArm:
                        ResolveExplosionCell(scheduled.SourceEntityId, scheduled.Position, events);
                        if (scheduled.Remaining > 1
                            && !CellQuery.HoldsBlastSurvivor(_world, scheduled.Position))
                        {
                            ScheduleCrossArm(
                                scheduled.SourceEntityId,
                                scheduled.Position,
                                scheduled.ArmDirection!.Value,
                                scheduled.Remaining - 1,
                                ExplosionDelaySeconds);
                        }

                        break;
                    case ScheduledCellKind.Blast:
                    default:
                        ResolveExplosionCell(scheduled.SourceEntityId, scheduled.Position, events);
                        break;
                }
            }

            return true;
        }

        private void StartExplosion(Entity source, List<SimulationEvent> events)
        {
            GridPosition center = source.Position;
            if (IsMonster(source))
            {
                if (_world.Entities.All(entity => entity.Id != source.Id))
                {
                    return;
                }

                events.Add(new ExplosionStarted(source.Id, center));
                DestroyMonster(source, source.Id, SpawnsBeetleOnDeath(source), events);
            }
            else
            {
                if (!_world.Remove(source.Id))
                {
                    return;
                }

                events.Add(new ExplosionStarted(source.Id, center));
                events.Add(new EntityDestroyed(source.Id, source.Id));
            }

            ResolveExplosionCell(source.Id, center, events);

            double dueTime = _world.CurrentTimeSeconds + ExplosionDelaySeconds;
            foreach ((int offsetX, int offsetY) in ExplosionNeighborOffsets)
            {
                GridPosition position = new(center.X + offsetX, center.Y + offsetY);
                if (_world.Contains(position))
                {
                    _pendingExplosionCells.Add(new ScheduledExplosionCell(
                        source.Id,
                        position,
                        dueTime,
                        _nextExplosionOrder++));
                }
            }
        }

        /// <summary>
        /// Gets whether a reached blast cell shows the blast graphic. The recovered
        /// <c>_ObjExplWrd</c> draws <c>SfxExplAdd1x1</c> only where no still, visible occupant
        /// survived, the same test that stops a cross-blast arm, so a presentation layer asks here
        /// rather than restating it.
        /// </summary>
        /// <param name="world">The authoritative world.</param>
        /// <param name="cell">The reached cell, tested after the step that reached it.</param>
        /// <returns><see langword="true"/> when the blast graphic is drawn.</returns>
        public static bool ShowsBlast(WorldGrid world, GridPosition cell)
        {
            ArgumentNullException.ThrowIfNull(world);
            return world.Contains(cell) && !CellQuery.HoldsBlastSurvivor(world, cell);
        }

        /// <summary>
        /// Applies the recovered <c>Game.ObjExplCross</c>, the barrel's blast. The barrel dies and
        /// its own cell is reached at once; after <c>CNT_GPrmExplCrossDelay</c> an arm leaves in
        /// each direction, left, up, right and down, and each step reaches the next cell
        /// <c>CNT_GPrmExplDelay</c> later until <c>CNT_GPrmExplCrossLen</c> cells are reached or
        /// a still, visible occupant survives in the cell just reached.
        /// </summary>
        private void StartCrossExplosion(Entity source, List<SimulationEvent> events)
        {
            GridPosition center = source.Position;
            if (!_world.Remove(source.Id))
            {
                return;
            }

            events.Add(new ExplosionStarted(source.Id, center));
            events.Add(new EntityDestroyed(source.Id, source.Id));
            ResolveExplosionCell(source.Id, center, events);
            foreach (Direction direction in (Direction[])[Direction.Left, Direction.Up, Direction.Right, Direction.Down])
            {
                ScheduleCrossArm(source.Id, center, direction, CrossBlastArmLength, CrossBlastDelaySeconds);
            }
        }

        private void ScheduleCrossArm(
            EntityId sourceEntityId,
            GridPosition from,
            Direction direction,
            int remaining,
            double delaySeconds)
        {
            GridPosition next = from.Offset(direction);
            if (!_world.Contains(next))
            {
                return;
            }

            _pendingExplosionCells.Add(new ScheduledExplosionCell(
                sourceEntityId,
                next,
                _world.CurrentTimeSeconds + delaySeconds,
                _nextExplosionOrder++,
                ScheduledCellKind.CrossArm,
                direction,
                remaining));
        }

        /// <summary>
        /// Applies the recovered <c>_DoMagic</c>: the gem dies and each cell of its 3x3 is worked
        /// after its Chebyshev distance times <c>CNT_WTime</c>, the gem's own cell at once.
        /// </summary>
        private void StartMagic(Entity gem, List<SimulationEvent> events)
        {
            GridPosition center = gem.Position;
            if (!_world.Remove(gem.Id))
            {
                return;
            }

            events.Add(new EntityDestroyed(gem.Id, gem.Id));
            double now = _world.CurrentTimeSeconds;
            for (int y = center.Y - 1; y <= center.Y + 1; y++)
            {
                for (int x = center.X - 1; x <= center.X + 1; x++)
                {
                    GridPosition cell = new(x, y);
                    if (cell == center || !_world.Contains(cell))
                    {
                        continue;
                    }

                    _pendingExplosionCells.Add(new ScheduledExplosionCell(
                        gem.Id,
                        cell,
                        now + MagicRingDelaySeconds,
                        _nextExplosionOrder++,
                        ScheduledCellKind.Magic));
                }
            }

            WorkMagicCell(gem.Id, center, events);
        }

        /// <summary>
        /// Applies the recovered <c>MagicWork</c> to one cell. A cell holding still treasure is
        /// left alone; otherwise its explodable occupants are killed and, if nothing still and
        /// visible remains, a random diamond is spawned there under the recovered kill lock.
        /// </summary>
        private void WorkMagicCell(EntityId gemId, GridPosition cell, List<SimulationEvent> events)
        {
            if (!_world.Contains(cell)
                || _world.GetOccupants(cell).Any(occupant => occupant.IsTreasure && occupant.Motion is null))
            {
                return;
            }

            KillExplodables(gemId, cell, events);
            if (CellQuery.IsOccupied(_world, cell))
            {
                return;
            }

            Entity diamond = LevelMaterializer.CreateRuntimeObject(
                CreateSpawnedEntityId(gemId),
                cell,
                MagicSpawnTypes[_random.Next(MagicSpawnTypes.Length)],
                _difficultySpeeds);
            diamond.KillLockedUntilSeconds = _world.CurrentTimeSeconds + MagicKillLockSeconds;
            _world.Add(diamond);
            events.Add(new MagicDiamondSpawned(gemId, diamond.Id, cell));
        }

        /// <summary>
        /// Starts whichever blast an explosive entity owns, the recovered handler its
        /// <c>OnShake</c>, <c>OnExpl</c> and <c>OnLand</c> name.
        /// </summary>
        private void StartOwnBlast(Entity source, List<SimulationEvent> events)
        {
            if (source.ExplosionBody?.Reaction == ExplosionReaction.CrossBlast)
            {
                StartCrossExplosion(source, events);
            }
            else if (source.ExplosionBody?.Reaction == ExplosionReaction.BigBomb)
            {
                StartBigBomb(source, events);
            }
            else
            {
                StartExplosion(source, events);
            }
        }

        /// <summary>
        /// Applies the recovered <c>BBombWork</c>: the bomb dies and its flood starts in its own
        /// cell with <c>CNT_BBombEn</c>. The bomb's own cell is not marked in the visited field, so
        /// the first ring sends the flood back into it once more.
        /// </summary>
        private void StartBigBomb(Entity bomb, List<SimulationEvent> events)
        {
            GridPosition center = bomb.Position;
            if (!_world.Remove(bomb.Id))
            {
                return;
            }

            events.Add(new ExplosionStarted(bomb.Id, center));
            events.Add(new EntityDestroyed(bomb.Id, bomb.Id));
            WorkBigBombCell(bomb.Id, center, BigBombEnergy, [], events);
        }

        /// <summary>
        /// Applies one recovered <c>BBombWork</c> step. The cell is blasted by <c>_ObjExplWrd</c>,
        /// which costs <c>CNT_BBombEnKill</c> when the cell held an explodable and
        /// <c>CNT_BBombEnEmpty</c> otherwise; with energy left, each unvisited neighbor, left, up,
        /// right and down, is marked visited and, unless it holds a visible occupant that cannot
        /// explode, worked <c>CNT_BBombChDelay</c> later.
        /// </summary>
        private void WorkBigBombCell(
            EntityId bombId,
            GridPosition cell,
            int energy,
            HashSet<GridPosition> flood,
            List<SimulationEvent> events)
        {
            bool held = _world.GetOccupants(cell).Any(occupant => occupant.IsExplodable);
            ResolveExplosionCell(bombId, cell, events);
            int remaining = energy - (held ? BigBombHitCost : 1);
            if (remaining <= 0)
            {
                return;
            }

            foreach (Direction direction in (Direction[])[Direction.Left, Direction.Up, Direction.Right, Direction.Down])
            {
                GridPosition next = cell.Offset(direction);
                if (!_world.Contains(next)
                    || !flood.Add(next)
                    || CellQuery.VisibleOccupants(_world, next).Any(occupant => !occupant.IsExplodable))
                {
                    continue;
                }

                _pendingExplosionCells.Add(new ScheduledExplosionCell(
                    bombId,
                    next,
                    _world.CurrentTimeSeconds + BigBombStepSeconds,
                    _nextExplosionOrder++,
                    ScheduledCellKind.BigBomb,
                    Remaining: remaining,
                    Flood: flood));
            }
        }

        /// <summary>
        /// Gives every bio mass its opening <c>ST_Stay</c>: a random frame time and, from the
        /// constructor's <c>Rand.FLim</c>, a random point part way through the loop.
        /// </summary>
        private void StartBioMasses()
        {
            foreach (Entity entity in _world.Entities)
            {
                if (entity.BioMassBody is { IsStarted: false } body)
                {
                    EnterBioStay(body);
                    body.RemainingSeconds *= 1 - _random.NextDouble();
                }
            }
        }

        /// <summary>
        /// Enters <c>ST_Stay</c> from another state, which picks a new frame time and restarts the
        /// loop.
        /// </summary>
        private void EnterBioStay(BioMassBody body)
        {
            body.IsStarted = true;
            body.State = BioMassState.Stay;
            body.StayFrameSeconds = _random.Next(2) == 0 ? 0.045 : 0.055;
            body.RemainingSeconds = BioStayFrames * body.StayFrameSeconds;
        }

        private double? SecondsUntilNextBioMass()
        {
            double? result = null;
            foreach (Entity entity in _world.Entities)
            {
                if (entity.BioMassBody is { IsStarted: true } body
                    && entity.Motion is null
                    && (result is null || body.RemainingSeconds < result))
                {
                    result = body.RemainingSeconds;
                }
            }

            return result;
        }

        /// <summary>Plays every bio mass's animation and applies each state end it reaches.</summary>
        private void AdvanceBioMasses(double elapsedSeconds, List<SimulationEvent> events)
        {
            Entity[] bios = [.. _world.Entities.Where(entity => entity.BioMassBody is { IsStarted: true })];
            foreach (Entity bio in bios)
            {
                BioMassBody body = bio.BioMassBody!;
                if (!_world.Entities.Contains(bio) || bio.Motion is not null)
                {
                    continue;
                }

                body.RemainingSeconds -= elapsedSeconds;
                if (body.RemainingSeconds > TimeTolerance)
                {
                    continue;
                }

                switch (body.State)
                {
                    case BioMassState.Stay:
                        // _AEnd_SLook: Rand.Prob then a spawn state when any side is open or holds
                        // a target. Otherwise the loop simply plays again at the same speed.
                        if (_random.Next(32768) <= BioSpawnProbability && BioSpawnSides(bio).Count != 0)
                        {
                            body.State = BioMassState.Spawn;
                            body.RemainingSeconds = BioSpawnSeconds;
                        }
                        else
                        {
                            body.RemainingSeconds = BioStayFrames * body.StayFrameSeconds;
                        }

                        break;
                    case BioMassState.Spawn:
                        SpreadBioMass(bio, events);
                        EnterBioStay(body);
                        break;
                    case BioMassState.Grow:
                    default:
                        EnterBioStay(body);
                        break;
                }
            }
        }

        /// <summary>
        /// Lists the sides <c>_AEnd_SLook</c> and <c>_AEnd_SWork</c> accept, left, up, right and
        /// down: a cell with no still, visible occupant, or one holding a bio-strike target.
        /// </summary>
        private List<Direction> BioSpawnSides(Entity bio)
        {
            List<Direction> sides = [];
            foreach (Direction direction in (Direction[])[Direction.Left, Direction.Up, Direction.Right, Direction.Down])
            {
                GridPosition cell = bio.Position.Offset(direction);
                if (!_world.Contains(cell))
                {
                    continue;
                }

                IReadOnlyList<Entity> occupants = _world.GetOccupants(cell);
                bool open = !occupants.Any(
                    occupant => occupant.Motion is null
                        && (occupant.Interactions
                            & (InteractionMask.OccupancyTransparent | InteractionMask.MonsterTransparent)) == 0);
                if (open || occupants.Any(occupant => occupant.IsBioMassTarget))
                {
                    sides.Add(direction);
                }
            }

            return sides;
        }

        /// <summary>
        /// Applies the recovered <c>_AEnd_SWork</c>: one accepted side is chosen at random, and every
        /// bio-strike target in it runs its <c>OnShake</c> or dies; only a side with no target
        /// receives a new bio mass, which starts in the parent's cell and slides in.
        /// </summary>
        private void SpreadBioMass(Entity bio, List<SimulationEvent> events)
        {
            List<Direction> sides = BioSpawnSides(bio);
            if (sides.Count == 0)
            {
                return;
            }

            Direction direction = sides[_random.Next(sides.Count)];
            GridPosition cell = bio.Position.Offset(direction);
            Entity[] targets = [.. _world.GetOccupants(cell).Where(occupant => occupant.IsBioMassTarget)];
            if (targets.Length != 0)
            {
                foreach (Entity target in targets)
                {
                    if (_world.Entities.Contains(target))
                    {
                        ShakeOrKill(target, bio.Id, events);
                    }
                }

                return;
            }

            Entity child = LevelMaterializer.CreateRuntimeObject(
                CreateSpawnedEntityId(bio.Id),
                bio.Position,
                BioMassType,
                _difficultySpeeds);
            BioMassBody childBody = child.BioMassBody!;
            childBody.IsStarted = true;
            childBody.State = BioMassState.Grow;
            childBody.RemainingSeconds = BioGrowSeconds;
            _world.Add(child);
            _world.CommitMoves([new PlannedMove(child.Id, bio.Position, cell)]);
            KinematicsSystem.Start(
                child,
                MotionCause.BioSpread,
                direction,
                _difficultySpeeds.NormalSpeed * BioSpreadSpeedMultiplier);
            events.Add(new BioMassSpread(bio.Id, child.Id, bio.Position, cell));
        }

        /// <summary>
        /// Applies the recovered <c>if not ObjDoSome(x, 'OnShake'): ObjKill(x)</c> that the rocket's
        /// collision and the bio mass's strike share. The player's <c>OnShake</c> is shake damage.
        /// </summary>
        private void ShakeOrKill(Entity target, EntityId sourceEntityId, List<SimulationEvent> events)
        {
            if (target.IsDamageablePlayer)
            {
                ApplyDamage(
                    new DamageRequest(sourceEntityId, target.Id, DamageKind.Shake, _difficultySpeeds.ShakeDamage),
                    events);
            }
            else if (target.ShakeBody is { OrdinaryReaction: not ShakeReaction.None } || IsMonster(target))
            {
                ApplyShake(target, sourceEntityId, hard: false, events);
            }
            else
            {
                DestroyEntity(target, sourceEntityId, events);
            }
        }

        void IPlayerActionEffects.FireRocket(GridPosition cell, Direction direction, List<SimulationEvent> events)
        {
            Entity player = _world.Player
                ?? throw new InvalidOperationException("Firing a rocket requires a player.");
            ClearWeaponCell(cell, events);

            // The recovered rocket mask is CellCanExplode | ShadowWeap | MonShadow: a shadow that
            // ordinary queries and monsters ignore, and whose OnExpl is its own 3x3 blast.
            Entity rocket = new(
                CreateSpawnedEntityId(player.Id),
                cell,
                InteractionMask.MonsterTransparent | InteractionMask.OccupancyTransparent,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, detonatesOnLanding: false),
                rocketBody: new RocketBody(direction));
            _world.Add(rocket);
            events.Add(new RocketFired(player.Id, rocket.Id, cell, direction));
            AdvanceRocketFlight(rocket, events);
        }

        /// <summary>
        /// Applies the recovered <c>CGameWeapRocket.DoFly</c>, the decision a rocket takes when it
        /// is fired and on every cell it reaches. A visible occupant ahead stops it: it runs its own
        /// 3x3 blast and shakes the cell ahead. Otherwise it flies on at
        /// <c>CNT_OPrmRocketSpeed</c> times the normal speed. Unlike the arrow it never bores
        /// through terrain.
        /// </summary>
        private void AdvanceRocketFlight(Entity rocket, List<SimulationEvent> events)
        {
            Direction direction = rocket.RocketBody!.Direction;
            GridPosition ahead = rocket.Position.Offset(direction);
            if (!_world.Contains(ahead))
            {
                StartExplosion(rocket, events);
                return;
            }

            if (CellQuery.IsOccupied(_world, ahead))
            {
                StartExplosion(rocket, events);
                ShakeCell(ahead, rocket.Id, hard: false, events);
                return;
            }

            double speed = _difficultySpeeds.NormalSpeed * ArrowFlightSpeedMultiplier;
            if (CellQuery.HasCounterMotion(_world, ahead, direction))
            {
                speed *= CounterMotionMultiplier;
            }

            _world.CommitMoves([new PlannedMove(rocket.Id, rocket.Position, ahead)]);
            KinematicsSystem.Start(rocket, MotionCause.RocketFlight, direction, speed);
        }

        void IPlayerActionEffects.LayMine(GridPosition cell, List<SimulationEvent> events)
        {
            Entity player = _world.Player
                ?? throw new InvalidOperationException("Laying a mine requires a player.");
            ClearWeaponCell(cell, events);
            Entity mine = LevelMaterializer.CreateRuntimeObject(
                CreateSpawnedEntityId(player.Id),
                cell,
                MineType,
                _difficultySpeeds);
            _world.Add(mine);
            events.Add(new MineLaid(player.Id, mine.Id, cell));
        }

        /// <summary>
        /// Applies the recovered collision pass, <c>sub_4A2F30</c>: every object cycle, an object with
        /// a collision mask runs its handler for each live co-occupant of its committed cell whose
        /// mask intersects it. A deployed mine's mask is <c>CNT_CMaskCellLockMotion</c> and its
        /// handler is its own 3x3 blast.
        /// </summary>
        private void ResolveWeaponCollisions(List<SimulationEvent> events)
        {
            Entity[] rockets = [.. _world.Entities.Where(entity => entity.RocketBody is not null)];
            foreach (Entity rocket in rockets)
            {
                Entity? other = _world.Entities.Contains(rocket)
                    ? _world.GetOccupants(rocket.Position).FirstOrDefault(
                        occupant => occupant.Id != rocket.Id
                            && (occupant.Interactions.HasFlag(InteractionMask.BlocksMovement)
                                || occupant.RocketBody is not null
                                || occupant.ArchivalIdentity?.Kind == EntityKind.Mine))
                    : null;
                if (other is not null)
                {
                    CollideRocket(rocket, other, events);
                }
            }

            Entity[] mines = [.. _world.Entities.Where(entity => entity.ArchivalIdentity?.Kind == EntityKind.Mine)];
            foreach (Entity mine in mines)
            {
                if (_world.Entities.Contains(mine)
                    && _world.GetOccupants(mine.Position).Any(
                        occupant => occupant.Id != mine.Id
                            && occupant.Interactions.HasFlag(InteractionMask.BlocksMovement)))
                {
                    StartExplosion(mine, events);
                }
            }
        }

        /// <summary>
        /// Applies the recovered <c>CGameWeapRocket.OnColl</c>: the rocket runs its 3x3 blast, and
        /// the other object, if that left it alive and it can explode, runs its <c>OnShake</c> or,
        /// lacking one, dies.
        /// </summary>
        private void CollideRocket(Entity rocket, Entity other, List<SimulationEvent> events)
        {
            StartExplosion(rocket, events);
            if (_world.Entities.Contains(other) && other.IsExplodable)
            {
                ShakeOrKill(other, rocket.Id, events);
            }
        }

        void IPlayerActionEffects.PlaceBigBomb(GridPosition cell, List<SimulationEvent> events)
        {
            Entity player = _world.Player
                ?? throw new InvalidOperationException("Placing a big bomb requires a player.");

            // The recovered TypeBBomb: RoundCap, BlueStrike, CellLockMotion, CellCanExplode and
            // PlCanPush, falling at the normal speed, with BBombWork for OnShake, OnShakeHard,
            // OnLand and OnExpl alike.
            Entity bomb = new(
                CreateSpawnedEntityId(player.Id),
                cell,
                InteractionMask.BlocksMovement
                    | InteractionMask.PlayerPushable
                    | InteractionMask.RoundSupport
                    | InteractionMask.BlueMonsterTarget,
                new GravityBody(_difficultySpeeds.NormalSpeed, canSlide: true),
                explosionBody: new ExplosionBody(ExplosionReaction.BigBomb, detonatesOnLanding: false),
                shakeBody: new ShakeBody(ShakeReaction.BigBomb, ShakeReaction.BigBomb, reactsToLanding: true),
                bigBombBody: new BigBombBody());
            _world.Add(bomb);
            events.Add(new BigBombPlaced(player.Id, bomb.Id, cell));
        }

        private double? SecondsUntilNextBigBomb()
        {
            double? result = null;
            foreach (Entity entity in _world.Entities)
            {
                if (entity.BigBombBody?.FuseRemainingSeconds is double remaining
                    && (result is null || remaining < result))
                {
                    result = remaining;
                }
            }

            return result;
        }

        /// <summary>Burns every placed big bomb's fuse and works each one that runs out.</summary>
        private void AdvanceBigBombFuses(double elapsedSeconds, List<SimulationEvent> events)
        {
            Entity[] bombs = [.. _world.Entities.Where(entity => entity.BigBombBody is not null)];
            foreach (Entity bomb in bombs)
            {
                BigBombBody body = bomb.BigBombBody!;
                body.FuseRemainingSeconds -= elapsedSeconds;
                if (body.FuseRemainingSeconds <= TimeTolerance)
                {
                    StartBigBomb(bomb, events);
                }
            }
        }

        /// <summary>
        /// Applies the recovered <c>Game._ObjExplWrd</c> to one cell without scheduling any
        /// neighbours, which is what a teleport arrival into an occupied cell runs.
        /// </summary>
        /// <param name="cell">The cell the blast reaches.</param>
        /// <param name="sourceEntityId">The entity the blast is attributed to.</param>
        /// <param name="events">The ordered event sink.</param>
        public void ExplodeCell(
            GridPosition cell,
            EntityId sourceEntityId,
            List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            if (_world.Contains(cell))
            {
                ResolveExplosionCell(sourceEntityId, cell, events);
            }
        }

        private void ResolveExplosionCell(
            EntityId sourceEntityId,
            GridPosition position,
            List<SimulationEvent> events)
        {
            events.Add(new ExplosionCellReached(sourceEntityId, position));
            _world.LockCell(position, ExplosionCellLockDuration);
            KillExplodables(sourceEntityId, position, events);
        }

        /// <summary>
        /// Applies the recovered <c>Game._ObjKillWrd</c>: every <c>CNT_CMaskCellCanExplode</c>
        /// carrier in the cell runs its <c>OnExpl</c>, which hurts the player, kills monsters and
        /// sets off other explosives. Unlike <c>_ObjExplWrd</c> it neither locks the cell nor counts
        /// as a blast reaching it, which is how the magic gem uses it.
        /// </summary>
        private void KillExplodables(
            EntityId sourceEntityId,
            GridPosition position,
            List<SimulationEvent> events)
        {
            foreach (Entity occupant in _world.GetOccupants(position))
            {
                if (occupant.IsDamageablePlayer)
                {
                    ApplyDamage(new DamageRequest(
                        sourceEntityId,
                        occupant.Id,
                        DamageKind.Explosion,
                        _difficultySpeeds.ExplosionDamage), events);
                    continue;
                }

                if (IsMonster(occupant))
                {
                    DestroyMonster(occupant, sourceEntityId, SpawnsBeetleOnDeath(occupant), events);
                    continue;
                }

                // A beetle is immune to blasts for the recovered CNT_ImTime after it spawns, so a
                // monster's own death blast cannot kill the child it just spawned.
                if (occupant.SmallBeetleBody is { RemainingImmunity: var immunity }
                    && immunity > TimeSpan.Zero)
                {
                    continue;
                }

                if (occupant.ExplosionBody?.Reaction == ExplosionReaction.Destroy)
                {
                    DestroyEntity(occupant, sourceEntityId, events);
                }
                else if (occupant.ExplosionBody?.Reaction == ExplosionReaction.Detonate3x3)
                {
                    StartExplosion(occupant, events);
                }
                else if (occupant.ExplosionBody?.Reaction == ExplosionReaction.CrossBlast)
                {
                    StartCrossExplosion(occupant, events);
                }
                else if (occupant.ExplosionBody?.Reaction == ExplosionReaction.BigBomb)
                {
                    StartBigBomb(occupant, events);
                }
                else if (occupant.ExplosionBody?.Reaction == ExplosionReaction.LaunchArrow)
                {
                    LaunchArrow(occupant, events);
                }
            }
        }

        /// <summary>
        /// Removes one entity. A monster is routed through <see cref="DestroyMonster"/> so no
        /// path can remove one without running its recovered <c>_OnKillInt</c> spawn.
        /// </summary>
        private void DestroyEntity(Entity entity, EntityId sourceEntityId, List<SimulationEvent> events)
        {
            if (entity.IsKillLocked(_world.CurrentTimeSeconds))
            {
                return;
            }

            if (IsMonster(entity))
            {
                DestroyMonster(entity, sourceEntityId, SpawnsBeetleOnDeath(entity), events);
                return;
            }

            if (_world.Remove(entity.Id))
            {
                events.Add(new EntityDestroyed(entity.Id, sourceEntityId));
                if (entity.IsTreasure)
                {
                    _world.Player?.PlayerBody?.Progress.ForfeitTreasureBonus();
                }
            }
        }

        private static bool IsMonster(Entity entity)
        {
            return entity.GreenMonsterBody is not null || entity.BlueMonsterBody is not null;
        }

        /// <summary>
        /// Applies the recovered <c>_DoFlyAll</c>. The arrow stops participating in gravity for
        /// good, exactly as the original's dropped <c>WEvOnFall</c> and <c>WEvOnSlide</c>
        /// registrations do, and enters the <c>ST_Work</c> windup. A second trigger while it is
        /// already launched changes nothing, because the original would only re-enter the state
        /// it is already in.
        /// </summary>
        public void LaunchArrow(Entity arrow, List<SimulationEvent> events)
        {
            if (arrow.FlyingArrowBody is not FlyingArrowBody body || body.IsLaunched)
            {
                return;
            }

            body.IsLaunched = true;
            body.WindupRemainingSeconds = ArrowWindupSeconds;
            arrow.GravityBody = null;
            KinematicsSystem.Stop(arrow);
            events.Add(new ArrowLaunched(
                arrow.Id,
                arrow.Position,
                body.Direction,
                TimeSpan.FromSeconds(ArrowWindupSeconds)));
        }

        /// <summary>
        /// Applies the recovered <c>GoTo_Work</c>: a struck rotator enters <c>ST_Work</c>, whose
        /// animation ends in <c>GMod.WorldRotStart</c>. Striking one that is already winding up
        /// restarts the animation, because the original re-enters the state it is already in.
        /// </summary>
        /// <param name="rotator">The struck rotator.</param>
        /// <param name="events">The ordered event sink.</param>
        public void TriggerWorldRotation(Entity rotator, List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(rotator);
            ArgumentNullException.ThrowIfNull(events);
            if (rotator.WorldRotationBody is not WorldRotationBody body)
            {
                return;
            }

            body.WindupRemainingSeconds = WorldRotationWindupSeconds;
        }

        private double? SecondsUntilNextRotatorWindup()
        {
            double? result = null;
            foreach (Entity entity in _world.Entities)
            {
                if (entity.WorldRotationBody?.WindupRemainingSeconds is double remaining
                    && (result is null || remaining < result))
                {
                    result = remaining;
                }
            }

            return result;
        }

        /// <summary>
        /// Ends the <c>ST_Work</c> windup of every rotator whose animation has run out and starts
        /// the world turning.
        /// </summary>
        /// <remarks>
        /// Two rotators can only finish together, never during a turn, because a turn freezes the
        /// simulation. When they do, the original's <c>WorldRotStart</c> simply overwrites the
        /// rotation state, so the last one decides the direction and only one turn happens.
        /// </remarks>
        private void AdvanceRotatorWindups(double elapsedSeconds, List<SimulationEvent> events)
        {
            Entity[] winding =
            [
                .. _world.Entities.Where(entity => entity.WorldRotationBody?.WindupRemainingSeconds is not null),
            ];
            foreach (Entity rotator in winding)
            {
                WorldRotationBody body = rotator.WorldRotationBody!;
                double remaining = body.WindupRemainingSeconds!.Value - elapsedSeconds;
                if (remaining > TimeTolerance)
                {
                    body.WindupRemainingSeconds = remaining;
                    continue;
                }

                // The recovered WorldRotLeft returns the object to ST_Stay straight away, so it is
                // ready to be struck again the moment the turn finishes.
                body.WindupRemainingSeconds = null;
                _rotationOwner = rotator.Id;
                _rotationDirection = body.Direction;
                _rotationRemainingSeconds = WorldRotationSpinSeconds;
                events.Add(new WorldRotationStarted(
                    rotator.Id,
                    body.Direction,
                    TimeSpan.FromSeconds(WorldRotationSpinSeconds)));
            }
        }

        /// <summary>
        /// Applies the recovered completion branch of <c>WorldRotCycle</c>. Every mid-motion object
        /// is finished at the destination cell it already owns before the grid is remapped, which
        /// is byte for byte the movement-completion block of the main object cycle: the trailing
        /// shift is zeroed, the arriving object is announced to its cell and its arrival handler
        /// runs. Only then does the world turn.
        /// </summary>
        private void CompleteWorldRotation(List<SimulationEvent> events)
        {
            EntityId owner = _rotationOwner!.Value;
            WorldRotationDirection direction = _rotationDirection;
            _rotationOwner = null;
            _rotationRemainingSeconds = 0;

            FinishMotionBeforeRotation(events);
            _world.Rotate(direction);
            events.Add(new WorldRotationCompleted(owner, direction, _world.Width, _world.Height));

            // Gravity never changes direction, so a turn is what moves the level relative to it and
            // everything standing still has to be re-checked. The original gets this for free: the
            // object cycle resumes and re-runs its fall check over every still object.
            _gravity.EvaluateStationary(_world, _gravityDirection, events);
        }

        private void FinishMotionBeforeRotation(List<SimulationEvent> events)
        {
            Entity[] moving = [.. _world.Entities.Where(entity => entity.Motion is not null)];
            if (moving.Length == 0)
            {
                return;
            }

            foreach (Entity entity in moving)
            {
                MotionState motion = entity.Motion!;
                motion.Progress = motion.TravelSign > 0 ? 1 : 0;
            }

            _ = ResolveBoundaries(events, out bool boundaryControlReady, out bool fromCellArrival);
            if (boundaryControlReady)
            {
                _playerController?.ResumeOrTryStart(events, fromCellArrival);
            }
        }

        private double? SecondsUntilNextArrowWindup()
        {
            double? result = null;
            foreach (Entity entity in _world.Entities)
            {
                if (entity.FlyingArrowBody?.WindupRemainingSeconds is double remaining
                    && (result is null || remaining < result))
                {
                    result = remaining;
                }
            }

            return result;
        }

        /// <summary>
        /// Ends the <c>ST_Work</c> windup of every arrow whose animation has run out, which is
        /// the recovered <c>_DlFlyBeg</c>: a still arrow takes its first flight decision at once.
        /// </summary>
        private void AdvanceArrowWindups(double elapsedSeconds, List<SimulationEvent> events)
        {
            Entity[] winding =
            [
                .. _world.Entities.Where(entity => entity.FlyingArrowBody?.WindupRemainingSeconds is not null),
            ];
            foreach (Entity arrow in winding)
            {
                FlyingArrowBody body = arrow.FlyingArrowBody!;
                double remaining = body.WindupRemainingSeconds!.Value - elapsedSeconds;
                if (remaining > TimeTolerance)
                {
                    body.WindupRemainingSeconds = remaining;
                    continue;
                }

                // The recovered _DlFlyBeg decides at once only for a still arrow; a moving one
                // decides when its current move arrives.
                body.WindupRemainingSeconds = null;
                body.IsFlying = true;
                if (arrow.Motion is null)
                {
                    AdvanceArrowFlight(arrow, events);
                }
            }
        }

        /// <summary>
        /// Applies the recovered <c>_DoFlyInt</c>, the decision an arrow takes in every cell it
        /// reaches. Everything ahead that carries <c>CNT_CMaskFlyThrought</c> is destroyed first;
        /// if the cell ahead still holds a visible occupant the arrow detonates its own 3x3 blast
        /// and shakes that cell, and otherwise it flies on at
        /// <c>CNT_OPrmRocketSpeed</c> times the normal speed.
        /// </summary>
        private void AdvanceArrowFlight(Entity arrow, List<SimulationEvent> events)
        {
            FlyingArrowBody body = arrow.FlyingArrowBody!;
            Direction direction = body.Direction;
            GridPosition ahead = arrow.Position.Offset(direction);
            if (!_world.Contains(ahead))
            {
                StartExplosion(arrow, events);
                return;
            }

            foreach (Entity occupant in _world.GetOccupants(ahead))
            {
                if (occupant.Id != arrow.Id
                    && occupant.Interactions.HasFlag(InteractionMask.DestroyedByFlyingArrow))
                {
                    DestroyEntity(occupant, arrow.Id, events);
                }
            }

            if (CellQuery.IsOccupied(_world, ahead))
            {
                StartExplosion(arrow, events);
                ShakeCell(ahead, arrow.Id, hard: false, events);
                return;
            }

            double speed = _difficultySpeeds.NormalSpeed * ArrowFlightSpeedMultiplier;
            if (CellQuery.HasCounterMotion(_world, ahead, direction))
            {
                speed *= CounterMotionMultiplier;
            }

            _world.CommitMoves([new PlannedMove(arrow.Id, arrow.Position, ahead)]);
            KinematicsSystem.Start(arrow, MotionCause.ArrowFlight, direction, speed);
        }

        /// <summary>
        /// Applies the recovered <c>_OnKillInt</c> suppression. The original skips the child while
        /// the monster is edible under the glasses bonus or while the ice bonus is active. The
        /// glasses bonus is not wired to Blue Monsters yet, so only ice suppresses a blue child.
        /// </summary>
        private static bool SpawnsBeetleOnDeath(Entity monster)
        {
            return monster.GreenMonsterBody is GreenMonsterBody green
                ? !green.IsFrozen && !green.IsSmall
                : monster.BlueMonsterBody is { IsFrozen: false };
        }

        /// <summary>
        /// Removes a defeated monster and applies its recovered <c>_OnKillInt</c> hook. Both
        /// monster families spawn a beetle of their own color at the cell the monster occupied,
        /// unless the kill came from a bonus that suppresses the child.
        /// </summary>
        private void DestroyMonster(
            Entity monster,
            EntityId sourceEntityId,
            bool spawnBeetle,
            List<SimulationEvent> events)
        {
            GridPosition position = monster.Motion is
            { Cause: MotionCause.MonsterWalking, Progress: < 0.5 } motion
                ? monster.Position.Offset(motion.SegmentDirection.Opposite())
                : monster.Position;
            SmallBeetleColor color = monster.BlueMonsterBody is not null
                ? SmallBeetleColor.Blue
                : SmallBeetleColor.Green;
            if (!_world.Remove(monster.Id))
            {
                return;
            }

            events.Add(new EntityDestroyed(monster.Id, sourceEntityId));
            _world.Player?.PlayerBody?.Progress.AddScore(MonsterKillScore);
            if (!spawnBeetle)
            {
                return;
            }

            // The recovered mask is ShadowObj | LvlEndLock | CellCanExplode | MonShadow. It holds
            // no CellLockMotion and no PlCanEat, so the beetle neither blocks the player nor is
            // eaten out of a cell; contact absorbs it instead.
            Entity beetle = new(
                CreateSpawnedEntityId(monster.Id),
                position,
                InteractionMask.MonsterTransparent
                    | InteractionMask.OccupancyTransparent
                    | InteractionMask.LevelEndLock,
                smallBeetleBody: new SmallBeetleBody(
                    color,
                    _difficultySpeeds.MonsterSpeed * BeetleSpeedMultiplier),
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, detonatesOnLanding: false));
            _world.Add(beetle);
            events.Add(new SmallBeetleSpawned(monster.Id, beetle.Id, position, color));
        }

        private EntityId CreateSpawnedEntityId(EntityId parentEntityId)
        {
            Span<byte> seed = stackalloc byte[24];
            Span<byte> hash = stackalloc byte[32];
            while (true)
            {
                seed.Clear();
                _ = parentEntityId.Value.TryWriteBytes(seed);
                BinaryPrimitives.WriteInt64LittleEndian(seed[16..], _nextSpawnOrder++);
                _ = SHA256.HashData(seed, hash);
                EntityId candidate = new(new Guid(hash[..16]));
                if (_world.Entities.All(entity => entity.Id != candidate))
                {
                    return candidate;
                }
            }
        }

        private void AdvanceMonsterTimers(double elapsedSeconds, List<SimulationEvent> events)
        {
            foreach (Entity entity in _world.Entities)
            {
                if (entity.GreenMonsterBody is not null)
                {
                    _greenMonsters.AdvanceTimer(
                        entity,
                        elapsedSeconds,
                        events,
                        request => ApplyDamage(request, events));
                }
                else if (entity.BlueMonsterBody is not null)
                {
                    _blueMonsters.AdvanceTimer(
                        entity,
                        elapsedSeconds,
                        events,
                        request => ApplyDamage(request, events),
                        (target, sourceId) => ApplyShake(target, sourceId, hard: false, events));
                }
            }

            DecideMonsters(events);
        }

        private void DecideMonsters(List<SimulationEvent> events)
        {
            IReadOnlyList<Entity> entities = _world.Entities;
            for (int index = 0; index < entities.Count; index++)
            {
                Entity entity = entities[index];
                if (entity.GreenMonsterBody is { IsFrozen: false, State: GreenMonsterState.Deciding })
                {
                    _greenMonsters.Decide(entity, index, events);
                }
                else if (entity.BlueMonsterBody is { IsFrozen: false, State: BlueMonsterState.Deciding })
                {
                    _blueMonsters.Decide(entity, index, events);
                }
                else if (entity.BlueMonsterBody is { IsFrozen: false, State: BlueMonsterState.DecidingAfterLeftTurn })
                {
                    _blueMonsters.DecideAfterLeftTurn(entity, index, events);
                }
                else if (entity.SmallBeetleBody?.State == SmallBeetleState.Deciding)
                {
                    _smallBeetles.Decide(entity, index, events);
                }
            }
        }

        /// <summary>
        /// Advances spawned beetles, removing any whose recovered lifetime has run out and
        /// absorbing any that now shares a cell with a live player.
        /// </summary>
        private void AdvanceBeetles(double elapsedSeconds, List<SimulationEvent> events)
        {
            foreach (Entity beetle in _world.Entities.Where(entity => entity.SmallBeetleBody is not null))
            {
                SmallBeetleSystem.AdvanceTimers(
                    beetle,
                    elapsedSeconds,
                    expired => DestroyEntity(expired, expired.Id, events));
            }

            AbsorbBeetles(events);
        }

        /// <summary>
        /// Applies the recovered <c>_PlHere</c> collision hook. The beetle carries
        /// <c>CNT_CMaskPlColl</c> and its flight test excludes the player, so it flies into the
        /// player's cell and is absorbed there rather than being eaten out of a blocking cell.
        /// </summary>
        private void AbsorbBeetles(List<SimulationEvent> events)
        {
            Entity? player = _world.Player;
            if (player is null || player.HealthBody is not HealthBody health || health.IsDead)
            {
                return;
            }

            Entity[] absorbed =
            [
                .. _world.Entities.Where(
                    entity => entity.SmallBeetleBody is not null && entity.Position == player.Position),
            ];
            foreach (Entity beetle in absorbed)
            {
                if (!_world.Remove(beetle.Id))
                {
                    continue;
                }

                player.PlayerBody?.Progress.AddScore(BeetleAbsorbScore);
                health.Heal(_difficultySpeeds.BeetleHeal);
                events.Add(new PlayerAte(player.Id, beetle.Id));
            }
        }

        void IPlayerActionEffects.ShatterFrozenMonster(Entity monster, List<SimulationEvent> events)
        {
            Entity player = _world.Player
                ?? throw new InvalidOperationException("A hand strike requires a player.");
            DestroyMonster(monster, player.Id, spawnBeetle: false, events);
        }

        /// <summary>
        /// Applies the recovered <c>CPlPushBase.OnPlPush</c>, which runs one step of the chain and
        /// offers <c>PlPush</c> only when that step did not stop the object against something.
        /// </summary>
        void IPlayerActionEffects.PushStruck(
            Entity target,
            Direction direction,
            List<SimulationEvent> events)
        {
            if (AdvanceStrikePush(target, direction, events))
            {
                LightBalloonFuse(target, events);
            }
        }

        void IPlayerActionEffects.OfferPush(Entity target, List<SimulationEvent> events)
        {
            LightBalloonFuse(target, events);
        }

        /// <summary>
        /// Applies the recovered <c>PlPush_Ballon</c>, the one <c>PlPush</c> handler among placed
        /// types. Both push paths reach it through here, so the one-shot rule is stated once.
        /// </summary>
        private static void LightBalloonFuse(Entity target, List<SimulationEvent> events)
        {
            if (target.BalloonFuseBody?.TryLight() == true)
            {
                events.Add(new BalloonFuseLit(target.Id, BalloonFuseBody.Duration));
            }
        }

        private double? SecondsUntilNextBalloonDetonation()
        {
            double? result = null;
            foreach (Entity entity in _world.Entities)
            {
                if (entity.BalloonFuseBody?.RemainingSeconds is double remaining
                    && (result is null || remaining < result))
                {
                    result = remaining;
                }
            }

            return result;
        }

        /// <summary>
        /// Burns every lit fuse and detonates each balloon whose <c>ST_Expl</c> has ended. Only the
        /// end of the chain is a substep boundary, because the intermediate tick states differ in
        /// their frames and sounds alone.
        /// </summary>
        private void AdvanceBalloonFuses(double elapsedSeconds, List<SimulationEvent> events)
        {
            Entity[] burning =
            [
                .. _world.Entities.Where(entity => entity.BalloonFuseBody?.RemainingSeconds is not null),
            ];
            foreach (Entity balloon in burning)
            {
                BalloonFuseBody body = balloon.BalloonFuseBody!;
                double remaining = body.RemainingSeconds!.Value - elapsedSeconds;
                if (remaining > TimeTolerance)
                {
                    body.RemainingSeconds = remaining;
                    continue;
                }

                body.RemainingSeconds = null;
                StartExplosion(balloon, events);
            }
        }

        /// <summary>
        /// Applies the recovered <c>Game._CellPlKill</c>: every crushable occupant of the cell is
        /// destroyed and the cell is locked for the explosion lock time.
        /// </summary>
        void IPlayerActionEffects.ClearWeaponCell(GridPosition cell, List<SimulationEvent> events)
        {
            ClearWeaponCell(cell, events);
        }

        void IPlayerActionEffects.PlaceGrenade(GridPosition cell, List<SimulationEvent> events)
        {
            Entity player = _world.Player
                ?? throw new InvalidOperationException("Placing a grenade requires a player.");

            // The recovered _AEnd_Bomb clears the cell a second time, because the cell may have
            // changed during the placing animation.
            ClearWeaponCell(cell, events);
            Entity grenade = LevelMaterializer.CreateRuntimeObject(
                CreateSpawnedEntityId(player.Id),
                cell,
                ThrownGrenadeType,
                _difficultySpeeds);
            _world.Add(grenade);
            events.Add(new PlayerGrenadePlaced(player.Id, grenade.Id, cell));
        }

        private void ClearWeaponCell(GridPosition cell, List<SimulationEvent> events)
        {
            if (!_world.Contains(cell))
            {
                return;
            }

            CrushCell(cell, events);
            _world.LockCell(cell, WeaponCellLockDuration);
        }

        /// <inheritdoc/>
        public void CrushCell(GridPosition cell, List<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            if (!_world.Contains(cell))
            {
                return;
            }

            foreach (Entity occupant in CellQuery.CrushableOccupants(_world, cell))
            {
                DestroyEntity(occupant, occupant.Id, events);
            }
        }

        /// <summary>
        /// Runs one step of the recovered <c>CPlPushBase.PlPush_OnCell</c> chain for a struck
        /// object. The object stops and falls when it is unsupported, consumes crushable occupants
        /// ahead when it can push through them, and otherwise either enters the cell ahead or
        /// stops against it. The chain therefore continues cell after cell, unlike the single-cell
        /// push a walking player applies.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> when the object stopped against the cell ahead, which is the
        /// recovered <c>PlPush_OnCell</c> returning <see langword="true"/>.
        /// </returns>
        private bool AdvanceStrikePush(
            Entity entity,
            Direction direction,
            List<SimulationEvent> events)
        {
            // The original re-registers PlPush_OnCell as the arrival handler, but _OnFall replaces
            // it, so a struck object that becomes unsupported stops being pushed.
            if (_gravity.EvaluateStationary(_world, entity, _gravityDirection, events))
            {
                return true;
            }

            GridPosition ahead = entity.Position.Offset(direction);
            if (_world.Contains(ahead)
                && entity.Interactions.HasFlag(InteractionMask.PushesThroughOccupants))
            {
                foreach (Entity crushed in CellQuery.CrushableOccupants(_world, ahead))
                {
                    DestroyEntity(crushed, entity.Id, events);
                }
            }

            // The recovered stop gate is CellObjCheck(c, 0, 0, True), the same ordinary query the
            // walking push applies to the cell beyond.
            if (!_world.Contains(ahead)
                || !_world.IsCellUnlocked(ahead)
                || CellQuery.IsOccupied(_world, ahead))
            {
                ResolveStrikePushImpact(entity, ahead, events);
                return false;
            }

            _world.CommitMoves([new PlannedMove(entity.Id, entity.Position, ahead)]);
            KinematicsSystem.Start(
                entity,
                MotionCause.StrikePushed,
                direction,
                _difficultySpeeds.PlayerSpeed * PushSpeedMultiplier);
            events.Add(new PushStarted(entity.Id, direction));
            return true;
        }

        /// <summary>
        /// Applies the recovered end of the strike-push chain, which shakes the stopped object
        /// itself and then every occupant of the cell it stopped against. A struck bottle therefore
        /// explodes where it stops, and a struck rock detonates whatever it stops against.
        /// </summary>
        private void ResolveStrikePushImpact(
            Entity entity,
            GridPosition ahead,
            List<SimulationEvent> events)
        {
            ApplyShake(entity, entity.Id, hard: false, events);
            ShakeCell(ahead, entity.Id, hard: false, events);
        }

        private double? SecondsUntilPlayerReactionEnds()
        {
            PlayerReaction? reaction = _world.Player?.PlayerBody?.Reaction;
            return reaction?.Kind == PlayerReactionKind.Shocked
                ? reaction.Remaining.TotalSeconds
                : null;
        }

        private void AdvanceHealthImmunity(double elapsedSeconds)
        {
            TimeSpan elapsed = SimulationTime.Elapsed(elapsedSeconds);
            foreach (Entity entity in _world.Entities)
            {
                entity.HealthBody?.AdvanceImmunity(elapsed);
            }
        }

        private bool AdvancePlayerReaction(
            double elapsedSeconds,
            List<SimulationEvent> events)
        {
            Entity? player = _world.Player;
            PlayerReaction? reaction = player?.PlayerBody?.Reaction;
            if (player is null
                || reaction is null
                || reaction.Kind != PlayerReactionKind.Shocked)
            {
                return false;
            }

            TimeSpan elapsed = SimulationTime.Elapsed(elapsedSeconds);
            if (reaction.Remaining - elapsed > TimerTolerance)
            {
                reaction.Remaining -= elapsed;
                return false;
            }

            reaction.Kind = PlayerReactionKind.Normal;
            reaction.Remaining = TimeSpan.Zero;
            events.Add(new PlayerShockEnded(player.Id));
            return true;
        }

        private void ValidateMotionState()
        {
            Entity? player = _world.Player;
            foreach (Entity entity in _world.Entities)
            {
                if (entity.GreenMonsterBody is not null && entity.BlueMonsterBody is not null)
                {
                    throw new InvalidOperationException(
                        "An entity cannot have both green- and blue-monster bodies.");
                }

                MotionState? motion = entity.Motion;
                if (motion?.Cause == MotionCause.PlayerWalking
                    && (player is null || entity.Id != player.Id || entity.PlayerBody is null))
                {
                    throw new InvalidOperationException("Player walking motion must belong to the world's player.");
                }

                if (motion?.Cause == MotionCause.PlayerPushed
                    && player is not null
                    && entity.Id == player.Id)
                {
                    throw new InvalidOperationException("The player cannot have pushed-object motion.");
                }

                if (motion?.Cause == MotionCause.MonsterWalking
                    && entity.GreenMonsterBody is null
                    && entity.BlueMonsterBody is null
                    && entity.SmallBeetleBody is null)
                {
                    throw new InvalidOperationException("Monster walking motion requires a monster body.");
                }
            }
        }

        private bool ResolveBoundaries(
            List<SimulationEvent> events,
            out bool playerControlReady,
            out bool fromCellArrival)
        {
            playerControlReady = false;
            fromCellArrival = false;
            Entity[] ready =
            [
                .. _world.Entities
                    .Where(IsAtBoundary)
                    .OrderBy(entity => BoundaryPriority(entity.Motion!.Cause))
                    .ThenBy(entity => entity.Motion!.ResolutionOrder),
            ];
            if (ready.Length == 0)
            {
                return false;
            }

            foreach (Entity entity in ready)
            {
                if (entity.Motion is not MotionState motion)
                {
                    continue;
                }
                if (motion.Cause == MotionCause.PlayerWalking
                    && motion.TravelSign < 0)
                {
                    KinematicsSystem.Stop(entity);
                    _playerController!.EndSegment();
                    events.Add(new PlayerReturned(entity.Id, entity.Position));
                    playerControlReady = true;
                    continue;
                }

                if (motion.Cause == MotionCause.PlayerPushed)
                {
                    ResolvePushedBoundary(entity, events);
                    continue;
                }

                if (motion.Cause == MotionCause.StrikePushed)
                {
                    Direction pushDirection = motion.SegmentDirection;
                    events.Add(new CellEntered(entity.Id, entity.Position));
                    KinematicsSystem.Stop(entity);
                    if (ResolveFireContact(entity, events))
                    {
                        continue;
                    }

                    _ = AdvanceStrikePush(entity, pushDirection, events);
                    continue;
                }

                if (motion.Cause == MotionCause.PlayerWalking)
                {
                    ResolvePlayerBoundary(entity, events);
                    playerControlReady = true;
                    fromCellArrival = true;
                    continue;
                }

                if (motion.Cause == MotionCause.ArrowFlight)
                {
                    events.Add(new CellEntered(entity.Id, entity.Position));
                    KinematicsSystem.Stop(entity);
                    AdvanceArrowFlight(entity, events);
                    continue;
                }

                if (motion.Cause == MotionCause.BioSpread)
                {
                    events.Add(new CellEntered(entity.Id, entity.Position));
                    KinematicsSystem.Stop(entity);
                    continue;
                }

                if (motion.Cause == MotionCause.RocketFlight)
                {
                    events.Add(new CellEntered(entity.Id, entity.Position));
                    KinematicsSystem.Stop(entity);
                    AdvanceRocketFlight(entity, events);
                    continue;
                }

                if (motion.Cause == MotionCause.TunnelTransit)
                {
                    events.Add(new CellEntered(entity.Id, entity.Position));
                    _playerController!.ResolveTunnelArrival(events);
                    _ = ResolveFireContact(entity, events);
                    continue;
                }

                if (motion.Cause == MotionCause.MonsterWalking)
                {
                    GridPosition monsterDestination = entity.Position;
                    KinematicsSystem.Stop(entity);
                    if (entity.GreenMonsterBody is not null)
                    {
                        GreenMonsterSystem.CompleteWalking(entity);
                    }
                    else if (entity.SmallBeetleBody is not null)
                    {
                        SmallBeetleSystem.CompleteFlight(entity);
                    }
                    else
                    {
                        BlueMonsterSystem.CompleteWalking(entity);
                    }

                    events.Add(new CellEntered(entity.Id, monsterDestination));
                    _ = ResolveFireContact(entity, events);
                    continue;
                }

                GridPosition destination = entity.Position;
                motion.Progress = 0;
                events.Add(new CellEntered(entity.Id, destination));
                if (ResolveFireContact(entity, events))
                {
                    continue;
                }

                if (motion.Cause == MotionCause.Sliding)
                {
                    KinematicsSystem.Stop(entity);
                    continue;
                }

                GridPosition following = destination.Offset(_gravityDirection);
                if (!_gravity.CanEnter(_world, entity, following))
                {
                    KinematicsSystem.Stop(entity);
                    events.Add(new Landed(entity.Id, _gravityDirection));
                    ResolveFallingImpact(entity, following, events);
                    if (entity.ExplosionBody?.DetonatesOnLanding == true)
                    {
                        // The recovered ExplWork: the 3x3 blast, then the downward shake again.
                        StartExplosion(entity, events);
                        ResolveFallingImpact(entity, following, events);
                    }
                    else if (entity.ShakeBody?.ReactsToLanding == true)
                    {
                        // The barrel, the magic gem and the arrows name their shake handler as
                        // OnLand too, so a landing runs it with no repeated shake after it.
                        ApplyShake(entity, entity.Id, hard: false, events);
                    }
                }
                else
                {
                    _world.CommitMoves([new PlannedMove(entity.Id, destination, following)]);
                }
            }

            DecideMonsters(events);
            _gravity.EvaluateStationary(_world, _gravityDirection, events);
            return true;
        }

        private void ResolveFallingImpact(
            Entity source,
            GridPosition impactedPosition,
            List<SimulationEvent> events)
        {
            ShakeCell(
                impactedPosition,
                source.Id,
                source.GravityBody!.ImpactStrength == GravityImpactStrength.Hard,
                events);
        }

        /// <summary>
        /// Applies the recovered <c>Game.CellShakeDir</c>: every occupant of the cell, in cell order,
        /// runs its <c>OnShake</c> or <c>OnShakeHard</c>. The player's handlers are shake damage, so a
        /// player in the cell is hurt in its place in that order.
        /// </summary>
        private void ShakeCell(
            GridPosition cell,
            EntityId sourceEntityId,
            bool hard,
            List<SimulationEvent> events)
        {
            if (!_world.Contains(cell))
            {
                return;
            }

            foreach (Entity occupant in _world.GetOccupants(cell))
            {
                if (occupant.IsDamageablePlayer)
                {
                    ApplyDamage(
                        new DamageRequest(
                            sourceEntityId,
                            occupant.Id,
                            hard ? DamageKind.HardShake : DamageKind.Shake,
                            hard ? _difficultySpeeds.HardShakeDamage : _difficultySpeeds.ShakeDamage),
                        events);
                    continue;
                }

                ApplyShake(occupant, sourceEntityId, hard, events);
            }
        }

        /// <summary>
        /// Dispatches one occupant's recovered <c>OnShake</c> or <c>OnShakeHard</c> handler.
        /// Occupants that define neither are only nudged by the original and are left alone,
        /// and a deferred handler is a recovered specialization that is not implemented yet.
        /// </summary>
        private void ApplyShake(
            Entity occupant,
            EntityId sourceEntityId,
            bool hard,
            List<SimulationEvent> events)
        {
            // An iced monster is killed outright by the same shake that would otherwise make
            // it detonate, and it spawns no child.
            if (occupant.IsFrozenMonster)
            {
                DestroyMonster(occupant, sourceEntityId, spawnBeetle: false, events);
                return;
            }

            ShakeBody? shake = occupant.ShakeBody;
            if (shake is null)
            {
                return;
            }

            switch (hard ? shake.HardReaction : shake.OrdinaryReaction)
            {
                case ShakeReaction.Destroy:
                    DestroyEntity(occupant, sourceEntityId, events);
                    break;
                case ShakeReaction.Detonate3x3:
                    StartExplosion(occupant, events);
                    break;
                case ShakeReaction.LaunchArrow:
                    LaunchArrow(occupant, events);
                    break;
                case ShakeReaction.CrossBlast:
                    StartCrossExplosion(occupant, events);
                    break;
                case ShakeReaction.Magic:
                    StartMagic(occupant, events);
                    break;
                case ShakeReaction.BigBomb:
                    StartBigBomb(occupant, events);
                    break;
                case ShakeReaction.None:
                    break;
                default:
                    throw new InvalidOperationException("Undefined shake reaction.");
            }
        }

        private static bool IsAtBoundary(Entity entity)
        {
            MotionState? motion = entity.Motion;
            return motion is not null
                && ((motion.Cause == MotionCause.PlayerWalking
                        && motion.TravelSign < 0
                        && motion.Progress <= BoundaryTolerance)
                    || motion.Progress >= 1 - BoundaryTolerance);
        }

        private static int BoundaryPriority(MotionCause cause)
        {
            return cause switch
            {
                MotionCause.PlayerPushed or MotionCause.StrikePushed => 0,
                MotionCause.Falling or MotionCause.Sliding or MotionCause.ArrowFlight
                    or MotionCause.RocketFlight or MotionCause.BioSpread => 1,
                MotionCause.MonsterWalking => 2,
                MotionCause.PlayerWalking or MotionCause.TunnelTransit => 3,
                _ => throw new InvalidOperationException("Unknown motion cause."),
            };
        }

        /// <summary>Identifies which recovered timer handler a scheduled cell runs.</summary>
        private enum ScheduledCellKind
        {
            /// <summary>A delayed <c>_ObjExplWrd</c> cell of a 3x3 blast.</summary>
            Blast,

            /// <summary>One step of a <c>_ObjExplCross</c> arm.</summary>
            CrossArm,

            /// <summary>A <c>MagicWork</c> cell.</summary>
            Magic,

            /// <summary>One step of a <c>BBombWork</c> flood.</summary>
            BigBomb,
        }

        /// <summary>
        /// A cell waiting for its recovered delay. A cross-blast arm carries its direction and the
        /// cells it may still reach, counting this one; a big-bomb step carries its energy and the
        /// flood's shared visited field.
        /// </summary>
        private sealed record ScheduledExplosionCell(
            EntityId SourceEntityId,
            GridPosition Position,
            double DueTimeSeconds,
            long Order,
            ScheduledCellKind Kind = ScheduledCellKind.Blast,
            Direction? ArmDirection = null,
            int Remaining = 0,
            HashSet<GridPosition>? Flood = null);

        private void ResolvePushedBoundary(
            Entity entity,
            List<SimulationEvent> events)
        {
            if (entity.FlyingArrowBody is { IsLaunched: true } arrow)
            {
                arrow.Direction = entity.Motion!.SegmentDirection;
                if (arrow.IsFlying)
                {
                    events.Add(new CellEntered(entity.Id, entity.Position));
                    KinematicsSystem.Stop(entity);
                    AdvanceArrowFlight(entity, events);
                    return;
                }
            }

            GridPosition destination = entity.Position;
            events.Add(new CellEntered(entity.Id, destination));
            KinematicsSystem.Stop(entity);
            if (ResolveFireContact(entity, events))
            {
                return;
            }

            if (_gravity.EvaluateStationary(_world, entity, _gravityDirection, events))
            {
                return;
            }
        }

        private void ResolvePlayerBoundary(
            Entity player,
            List<SimulationEvent> events)
        {
            KinematicsSystem.Stop(player);
            _playerController!.EndSegment();

            events.Add(new CellEntered(player.Id, player.Position));
            _ = ResolveFireContact(player, events);
            if (!_playerController.TryStartPendingDrink(events))
            {
                DispatchPlayerHere(player, events);
            }
        }

        /// <summary>
        /// Applies the <c>PlHere</c> dispatch of the recovered <c>_OnCell_Wlk</c>, which offers it
        /// to every occupant of the player's own cell with the positive mask that bypasses the
        /// shadow filter. The message table is the only placed type that answers.
        /// </summary>
        private void DispatchPlayerHere(Entity player, List<SimulationEvent> events)
        {
            foreach (Entity occupant in _world.GetOccupants(player.Position))
            {
                if (occupant.MessageTableBody is not MessageTableBody table)
                {
                    continue;
                }

                table.HasBeenRead = true;
                if (table.HelpTextId is string helpTextId)
                {
                    events.Add(new HelpTextRequested(occupant.Id, helpTextId));
                }
            }
        }

        /// <summary>
        /// Applies the recovered <c>_ObjHere_Fire</c> to an entity that has just arrived in its
        /// cell. The handler is registered on the fire through <c>EvObjHere</c>, so it runs for
        /// the entity that entered and never when the fire itself arrives somewhere: fire falling
        /// onto a standing player burns nobody.
        /// </summary>
        /// <remarks>
        /// The recovered branches are taken in this order and each kills in this order. A player
        /// spends the fire and is then burned for <c>Compl.HFire</c>; a <c>bFireKill</c> carrier
        /// and the fire destroy each other; anything else is consumed while the fire survives.
        /// The native dispatch point of <c>WEvObjHere</c> could not be located in
        /// <c>mooh.exe.c</c>, so arrival is a structural reading: the relink that commits
        /// occupancy at movement start (<c>sub_4A0540</c>) carries no callback at all, and every
        /// other per-cell interaction in the engine — <c>PlHere</c>, <c>WEvOnCell</c> — resolves
        /// on arrival.
        /// </remarks>
        /// <returns><see langword="true"/> when the arriving entity was destroyed.</returns>
        private bool ResolveFireContact(Entity arriving, List<SimulationEvent> events)
        {
            if (arriving.FireBody?.Contact == FireContact.Source)
            {
                return false;
            }

            Entity? fire = _world.GetOccupants(arriving.Position).FirstOrDefault(
                occupant => occupant.FireBody?.Contact == FireContact.Source);
            if (fire is null)
            {
                return false;
            }

            // The recovered branch is isinstance(Other, CGameObjPlayer), not a mortality test, so
            // an immortal player still spends the fire and the damage request is what gets
            // rejected. A player inside a tunnel therefore puts the fire out and walks on.
            if (arriving.PlayerBody is not null && arriving.HealthBody is not null)
            {
                DestroyEntity(fire, arriving.Id, events);
                ApplyDamage(
                    new DamageRequest(
                        fire.Id,
                        arriving.Id,
                        DamageKind.Fire,
                        _difficultySpeeds.FireDamage),
                    events);
                return false;
            }

            DestroyEntity(arriving, fire.Id, events);
            if (arriving.FireBody?.Contact == FireContact.MutualDestruction)
            {
                DestroyEntity(fire, arriving.Id, events);
            }

            return true;
        }
    }
}
