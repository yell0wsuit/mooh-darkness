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
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Player;
using Mooh.Core.Shakes;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Coordinates continuous gravity evaluation and kinematic advancement for a world.
    /// </summary>
    public sealed class SimulationWorld
    {
        private const double BoundaryTolerance = 1e-6;
        private const double ExplosionDelaySeconds = 0.2;
        private const int MonsterKillScore = 10;
        private const int BeetleAbsorbScore = 90;

        /// <summary>
        /// The recovered <c>CNT_OPrm_SpeedSmBeetle</c> factor against <c>Compl.SpeedMon</c>. The
        /// original also scales each segment by <c>Rand.FRange(0.9, 1.1)</c>; that jitter is
        /// deliberately omitted so beetle flight stays reproducible.
        /// </summary>
        private const double BeetleSpeedMultiplier = 0.5;
        private const double TimeTolerance = 1e-12;
        private static readonly TimeSpan ExplosionCellLockDuration = TimeSpan.FromMilliseconds(500);
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
        private readonly SmallBeetleSystem _smallBeetles;
        private readonly WorldGrid _world;
        private Direction _gravityDirection;
        private long _nextExplosionOrder;
        private long _nextSpawnOrder;

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
            _movement = new MovementSystem(world, new DefaultInteractionPolicy());
            _blueMonsters = new BlueMonsterSystem(world);
            _greenMonsters = new GreenMonsterSystem(world);
            _smallBeetles = new SmallBeetleSystem(world);
            _levelOutcome = new LevelOutcomeSystem(world, _difficultySpeeds);
            _playerController = world.Player is Entity player
                ? new PlayerController(player, world, _movement, gravityDirection, _difficultySpeeds)
                : null;
        }

        /// <summary>
        /// Gets whether the level has been decided. The outcome becomes observable only after the
        /// recovered settle following the deciding condition.
        /// </summary>
        public LevelOutcome Outcome => _levelOutcome.Outcome;

        /// <summary>Replaces the currently held input for the world's player.</summary>
        /// <param name="input">The latest held cardinal inputs.</param>
        /// <exception cref="InvalidOperationException">The world has no player.</exception>
        public void SetPlayerInput(PlayerInputState input)
        {
            PlayerController controller = _playerController
                ?? throw new InvalidOperationException("Player input requires a world with a player.");
            controller.SetInput(input);
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
                SynchronizeGreenMonsterIce(events);
            }

            foreach (Entity monster in _world.Entities.Where(entity => entity.GreenMonsterBody is not null))
            {
                _greenMonsters.SetSmall(monster, active, events);
            }

            return events;
        }

        /// <summary>Starts a recovered delayed 3x3 explosion from a registered explosive entity.</summary>
        /// <param name="sourceEntityId">The entity that becomes the explosion center.</param>
        /// <returns>The immediate ordered events; neighboring cells remain scheduled.</returns>
        /// <exception cref="KeyNotFoundException">The source is not registered.</exception>
        /// <exception cref="InvalidOperationException">The source is not a 3x3 explosive.</exception>
        public IReadOnlyList<SimulationEvent> Detonate(EntityId sourceEntityId)
        {
            Entity source = _world.GetEntity(sourceEntityId);
            if (source.ExplosionBody?.Reaction != ExplosionReaction.Detonate3x3)
            {
                throw new InvalidOperationException("Detonation requires a 3x3 explosive entity.");
            }

            List<SimulationEvent> events = [];
            StartExplosion(source, events);
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
            _playerController?.EndSegment();
            if (target.HealthBody!.IsDead)
            {
                body.Reaction.Kind = PlayerReactionKind.Dead;
                body.Reaction.Remaining = TimeSpan.Zero;
                events.Add(new PlayerDied(target.Id));
            }
            else
            {
                body.Reaction.Kind = PlayerReactionKind.Shocked;
                body.Reaction.Remaining = body.ShockDuration;
                events.Add(new PlayerShockStarted(target.Id, body.ShockDuration));
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
            List<SimulationEvent> events = [];
            SynchronizeGreenMonsterIce(events);
            _gravity.EvaluateStationary(_world, _gravityDirection, events);
            DecideMonsters(events);
            _playerController?.TryStart(events);
            _levelOutcome.Observe();

            double remainingSeconds = elapsed.TotalSeconds;
            while (remainingSeconds > 0)
            {
                _playerController?.HandleInTransit(events);
                Entity[] moving = [.. _world.Entities.Where(entity => entity.Motion is not null)];
                double? rotationSeconds = _playerController?.RotationSecondsRemaining;
                double? unlockSeconds = _world.SecondsUntilNextCellUnlock();
                double? healthSeconds = SecondsUntilNextImmunityExpiry();
                double? bonusSeconds = SecondsUntilNextBonusExpiry();
                double? reactionSeconds = SecondsUntilPlayerReactionEnds();
                double? monsterSeconds = SecondsUntilNextMonsterTimer();
                double? explosionSeconds = SecondsUntilNextExplosionCell();
                double? beetleSeconds = _smallBeetles.SecondsUntilNextTimer();
                double? outcomeSeconds = _levelOutcome.SecondsUntilSettle();

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

                if (moving.Length == 0
                    && rotationSeconds is null
                    && unlockSeconds is null
                    && healthSeconds is null
                    && bonusSeconds is null
                    && reactionSeconds is null
                    && monsterSeconds is null
                    && explosionSeconds is null
                    && beetleSeconds is null
                    && outcomeSeconds is null)
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
                SynchronizeGreenMonsterIce(events);
                bool playerControlReady = _playerController?.AdvanceRotation(
                    substepSeconds,
                    events) == true;
                playerControlReady |= AdvancePlayerReaction(substepSeconds, events);
                // Beetles advance first, so a wait that expires this substep is decided by the
                // DecideMonsters pass at the end of AdvanceMonsterTimers rather than one substep
                // later.
                AdvanceBeetles(substepSeconds, events);
                AdvanceMonsterTimers(substepSeconds, events);
                bool resolvedExplosion = ResolvePendingExplosionCells(events);

                remainingSeconds -= substepSeconds;
                if (remainingSeconds < TimeTolerance)
                {
                    remainingSeconds = 0;
                }

                bool resolvedBoundary = ResolveBoundaries(
                    events,
                    out bool boundaryControlReady,
                    out bool emitStoppedWhenNoInput);
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

                if (playerControlReady)
                {
                    _playerController?.ResumeOrTryStart(events, emitStoppedWhenNoInput);
                }

                _levelOutcome.Advance(substepSeconds, events);
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
            _world.Player?.PlayerBody?.Progress.Advance(TimeSpan.FromSeconds(elapsedSeconds));
        }

        private void SynchronizeGreenMonsterIce(List<SimulationEvent> events)
        {
            bool iceActive = _world.Player?.PlayerBody?.Progress.IceRemaining > TimeSpan.Zero;
            foreach (Entity monster in _world.Entities.Where(entity => entity.GreenMonsterBody is not null))
            {
                if (iceActive)
                {
                    if (monster.GreenMonsterBody!.IsSmall)
                    {
                        _greenMonsters.SetSmall(monster, isSmall: false, events);
                    }

                    _greenMonsters.Freeze(monster, events);
                }
                else
                {
                    GreenMonsterSystem.Thaw(monster, events);
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
                ResolveExplosionCell(scheduled.SourceEntityId, scheduled.Position, events);
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

        private void ResolveExplosionCell(
            EntityId sourceEntityId,
            GridPosition position,
            List<SimulationEvent> events)
        {
            events.Add(new ExplosionCellReached(sourceEntityId, position));
            _world.LockCell(position, ExplosionCellLockDuration);
            foreach (Entity occupant in _world.GetOccupants(position))
            {
                if (occupant.PlayerBody is not null && occupant.HealthBody is not null)
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

                // ExplosionReaction.Deferred is deliberately inert. Those entities carry
                // CNT_CMaskCellCanExplode, so a blast does reach them, but their recovered
                // handlers — the barrel's cross blast and the gravity arrow's launch — are not
                // implemented yet.
                if (occupant.ExplosionBody?.Reaction == ExplosionReaction.Destroy)
                {
                    DestroyEntity(occupant, sourceEntityId, events);
                }
                else if (occupant.ExplosionBody?.Reaction == ExplosionReaction.Detonate3x3)
                {
                    StartExplosion(occupant, events);
                }
            }
        }

        /// <summary>
        /// Removes one entity. A monster is routed through <see cref="DestroyMonster"/> so no
        /// path can remove one without running its recovered <c>_OnKillInt</c> spawn.
        /// </summary>
        private void DestroyEntity(Entity entity, EntityId sourceEntityId, List<SimulationEvent> events)
        {
            if (IsMonster(entity))
            {
                DestroyMonster(entity, sourceEntityId, SpawnsBeetleOnDeath(entity), events);
                return;
            }

            if (_world.Remove(entity.Id))
            {
                events.Add(new EntityDestroyed(entity.Id, sourceEntityId));
            }
        }

        private static bool IsMonster(Entity entity)
        {
            return entity.GreenMonsterBody is not null || entity.BlueMonsterBody is not null;
        }

        /// <summary>
        /// Applies the recovered <c>_OnKillInt</c> suppression. The original skips the child while
        /// the monster is edible under the glasses bonus or while the ice bonus is active. Neither
        /// bonus is wired to Blue Monsters yet, so a Blue Monster always spawns its beetle.
        /// </summary>
        private static bool SpawnsBeetleOnDeath(Entity monster)
        {
            return monster.GreenMonsterBody is GreenMonsterBody green
                ? !green.IsFrozen && !green.IsSmall
                : monster.BlueMonsterBody is not null;
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
                InteractionMask.MonsterTransparent | InteractionMask.LevelEndLock,
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
                else if (entity.BlueMonsterBody?.State == BlueMonsterState.Deciding)
                {
                    _blueMonsters.Decide(entity, index, events);
                }
                else if (entity.BlueMonsterBody?.State == BlueMonsterState.DecidingAfterLeftTurn)
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

        private double? SecondsUntilPlayerReactionEnds()
        {
            PlayerReaction? reaction = _world.Player?.PlayerBody?.Reaction;
            return reaction?.Kind == PlayerReactionKind.Shocked
                ? reaction.Remaining.TotalSeconds
                : null;
        }

        private void AdvanceHealthImmunity(double elapsedSeconds)
        {
            TimeSpan elapsed = TimeSpan.FromSeconds(elapsedSeconds);
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

            TimeSpan elapsed = TimeSpan.FromSeconds(elapsedSeconds);
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
            out bool emitStoppedWhenNoInput)
        {
            playerControlReady = false;
            emitStoppedWhenNoInput = false;
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

                if (motion.Cause == MotionCause.PlayerWalking)
                {
                    ResolvePlayerBoundary(entity, events);
                    playerControlReady = true;
                    emitStoppedWhenNoInput = true;
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
                    continue;
                }

                GridPosition destination = entity.Position;
                motion.Progress = 0;
                events.Add(new CellEntered(entity.Id, destination));

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
                        StartExplosion(entity, events);
                        ResolveFallingImpact(entity, following, events);
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
            if (!_world.Contains(impactedPosition))
            {
                return;
            }

            GravityImpactStrength strength = source.GravityBody!.ImpactStrength;
            bool hard = strength == GravityImpactStrength.Hard;
            IReadOnlyList<Entity> impacted = _world.GetOccupants(impactedPosition);
            foreach (Entity occupant in impacted)
            {
                ApplyShake(occupant, source.Id, hard, events);
            }

            Entity? player = impacted.FirstOrDefault(
                occupant => occupant.PlayerBody is not null && occupant.HealthBody is not null);
            if (player is null)
            {
                return;
            }

            DamageKind kind = hard ? DamageKind.HardShake : DamageKind.Shake;
            double amount = hard
                ? _difficultySpeeds.HardShakeDamage
                : _difficultySpeeds.ShakeDamage;
            ApplyDamage(new DamageRequest(source.Id, player.Id, kind, amount), events);
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
            if (occupant.GreenMonsterBody is { IsFrozen: true })
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
                case ShakeReaction.None:
                case ShakeReaction.Deferred:
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
                MotionCause.PlayerPushed => 0,
                MotionCause.Falling or MotionCause.Sliding => 1,
                MotionCause.MonsterWalking => 2,
                MotionCause.PlayerWalking => 3,
                _ => throw new InvalidOperationException("Unknown motion cause."),
            };
        }

        private sealed record ScheduledExplosionCell(
            EntityId SourceEntityId,
            GridPosition Position,
            double DueTimeSeconds,
            long Order);

        private void ResolvePushedBoundary(
            Entity entity,
            List<SimulationEvent> events)
        {
            GridPosition destination = entity.Position;
            events.Add(new CellEntered(entity.Id, destination));
            KinematicsSystem.Stop(entity);

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
        }
    }
}
