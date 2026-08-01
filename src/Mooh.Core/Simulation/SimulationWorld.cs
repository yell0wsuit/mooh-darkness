using System;
using System.Collections.Generic;
using System.Linq;

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

        private readonly GravitySystem _gravity;
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
            _playerController?.TryStart(events);

            double remainingSeconds = elapsed.TotalSeconds;
            while (remainingSeconds > 0)
            {
                _playerController?.HandleInTransit(events);
                Entity[] moving = [.. _world.Entities.Where(entity => entity.Motion is not null)];
                double? rotationSeconds = _playerController?.RotationSecondsRemaining;
                double? unlockSeconds = _world.SecondsUntilNextCellUnlock();

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

                if (moving.Length == 0
                    && rotationSeconds is null
                    && unlockSeconds is null)
                {
                    _world.AdvanceTime(remainingSeconds);
                    break;
                }

                foreach (Entity entity in moving)
                {
                    KinematicsSystem.Advance(entity, substepSeconds);
                }

                _world.AdvanceTime(substepSeconds);
                _playerController?.AdvanceRotation(substepSeconds, events);

                remainingSeconds -= substepSeconds;
                if (remainingSeconds < TimeTolerance)
                {
                    remainingSeconds = 0;
                }

                bool resolvedBoundary = ResolveBoundaries(events);
                if (!resolvedBoundary
                    && unlockSeconds is double expiringLock
                    && substepSeconds >= expiringLock - TimeTolerance)
                {
                    _gravity.EvaluateStationary(_world, _gravityDirection, events);
                }
            }

            return events;
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
            }
        }

        private bool ResolveBoundaries(List<SimulationEvent> events)
        {
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
                    _playerController.TryStart(events);
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
                MotionCause.PlayerWalking => 2,
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
            _playerController.TryStart(events, emitStoppedWhenNoInput: true);
        }
    }
}
