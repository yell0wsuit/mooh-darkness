using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Player;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Coordinates continuous gravity evaluation and kinematic advancement for a world.
    /// </summary>
    public sealed class SimulationWorld
    {
        private const double BoundaryTolerance = 1e-6;
        private const double TimeTolerance = 1e-12;
        private static readonly TimeSpan TimerTolerance = TimeSpan.FromTicks(1);

        private readonly BlueMonsterSystem _blueMonsters;
        private readonly GravitySystem _gravity;
        private readonly GreenMonsterSystem _greenMonsters;
        private readonly double _maximumTravelPerSubstep;
        private readonly MovementSystem _movement;
        private readonly PlayerController? _playerController;
        private readonly WorldGrid _world;
        private Direction _gravityDirection;

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
                maximumTravelPerSubstep)
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
        {
            if (!double.IsFinite(maximumTravelPerSubstep)
                || maximumTravelPerSubstep <= 0
                || maximumTravelPerSubstep > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTravelPerSubstep));
            }

            _world = world ?? throw new ArgumentNullException(nameof(world));
            _gravityDirection = gravityDirection;
            _maximumTravelPerSubstep = maximumTravelPerSubstep;
            _gravity = new GravitySystem(collisionPolicy, random);
            _movement = new MovementSystem(world, new DefaultInteractionPolicy());
            _blueMonsters = new BlueMonsterSystem(world, new DefaultInteractionPolicy());
            _greenMonsters = new GreenMonsterSystem(world, new DefaultInteractionPolicy());
            _playerController = world.Player is Entity player
                ? new PlayerController(player, world, _movement, gravityDirection)
                : null;
        }

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
            _gravity.EvaluateStationary(_world, _gravityDirection, events);
            DecideMonsters(events);
            _playerController?.TryStart(events);

            double remainingSeconds = elapsed.TotalSeconds;
            while (remainingSeconds > 0)
            {
                _playerController?.HandleInTransit(events);
                Entity[] moving = [.. _world.Entities.Where(entity => entity.Motion is not null)];
                double? rotationSeconds = _playerController?.RotationSecondsRemaining;
                double? unlockSeconds = _world.SecondsUntilNextCellUnlock();
                double? healthSeconds = SecondsUntilNextImmunityExpiry();
                double? reactionSeconds = SecondsUntilPlayerReactionEnds();
                double? monsterSeconds = SecondsUntilNextMonsterTimer();

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

                if (reactionSeconds is double remainingReaction)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingReaction);
                }

                if (monsterSeconds is double remainingMonsterState)
                {
                    substepSeconds = Math.Min(substepSeconds, remainingMonsterState);
                }

                if (moving.Length == 0
                    && rotationSeconds is null
                    && unlockSeconds is null
                    && healthSeconds is null
                    && reactionSeconds is null
                    && monsterSeconds is null)
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
                bool playerControlReady = _playerController?.AdvanceRotation(
                    substepSeconds,
                    events) == true;
                playerControlReady |= AdvancePlayerReaction(substepSeconds, events);
                AdvanceMonsterTimers(substepSeconds, events);

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

                if (playerControlReady)
                {
                    _playerController?.ResumeOrTryStart(events, emitStoppedWhenNoInput);
                }
            }

            return events;
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
                    BlueMonsterSystem.AdvanceTimer(entity, elapsedSeconds, events);
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
                if (entity.GreenMonsterBody?.State == GreenMonsterState.Deciding)
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
                    && entity.BlueMonsterBody is null)
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
                MotionState motion = entity.Motion!;
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
