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
        private readonly DefaultInteractionPolicy _interactions;
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
            _interactions = new DefaultInteractionPolicy();
            _movement = new MovementSystem(world, _interactions);
            _playerController = world.Player is Entity player
                ? new PlayerController(player, world, _movement)
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
                if (moving.Length == 0)
                {
                    break;
                }

                double maximumSpeed = moving.Max(entity => entity.Motion!.Speed);
                double boundarySeconds = moving.Min(
                    entity => entity.Motion!.TravelSign > 0
                        ? (1 - entity.Motion.Progress) / entity.Motion.Speed
                        : entity.Motion.Progress / entity.Motion.Speed);
                double substepSeconds = Math.Min(
                    remainingSeconds,
                    Math.Min(_maximumTravelPerSubstep / maximumSpeed, boundarySeconds));

                foreach (Entity entity in moving)
                {
                    KinematicsSystem.Advance(entity, substepSeconds);
                }

                remainingSeconds -= substepSeconds;
                if (remainingSeconds < TimeTolerance)
                {
                    remainingSeconds = 0;
                }

                ResolveBoundaries(events);
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

        private void ResolveBoundaries(List<SimulationEvent> events)
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
                return;
            }

            HashSet<EntityId> readyPushedIds =
            [
                .. ready
                    .Where(entity => entity.Motion!.Cause == MotionCause.PlayerPushed)
                    .Select(entity => entity.Id),
            ];
            Dictionary<EntityId, bool> pushedCanCross = [];
            foreach (Entity pushed in ready.Where(
                entity => entity.Motion!.Cause == MotionCause.PlayerPushed))
            {
                pushedCanCross.Add(
                    pushed.Id,
                    CanPushedCross(pushed, pushed.Motion!, readyPushedIds, pushedCanCross));
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
                    ResolvePushedBoundary(entity, motion, pushedCanCross[entity.Id], events);
                    continue;
                }

                if (motion.Cause == MotionCause.PlayerWalking)
                {
                    ResolvePlayerBoundary(entity, motion, events);
                    continue;
                }

                GridPosition destination = entity.Position.Offset(motion.SegmentDirection);
                if (!_gravity.CanEnter(_world, entity, destination))
                {
                    KinematicsSystem.Stop(entity);
                    if (motion.Cause == MotionCause.Falling)
                    {
                        events.Add(new Landed(entity.Id, _gravityDirection));
                    }

                    continue;
                }

                _world.CommitMoves([new PlannedMove(entity.Id, entity.Position, destination)]);
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
            }

            _gravity.EvaluateStationary(_world, _gravityDirection, events);
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

        private bool CanPushedCross(
            Entity entity,
            MotionState motion,
            HashSet<EntityId> readyPushedIds,
            Dictionary<EntityId, bool> earlierResults)
        {
            GridPosition destination = entity.Position.Offset(motion.SegmentDirection);
            if (!_world.Contains(destination))
            {
                return false;
            }

            foreach (Entity occupant in _world.GetOccupants(destination))
            {
                if (readyPushedIds.Contains(occupant.Id))
                {
                    if (earlierResults.TryGetValue(occupant.Id, out bool willCross) && willCross)
                    {
                        continue;
                    }

                    return false;
                }

                if (_interactions.Classify(entity, occupant) != OccupantInteraction.Allow)
                {
                    return false;
                }
            }

            return true;
        }

        private void ResolvePushedBoundary(
            Entity entity,
            MotionState motion,
            bool canCross,
            List<SimulationEvent> events)
        {
            if (!canCross)
            {
                KinematicsSystem.Stop(entity);
                return;
            }

            Direction direction = motion.SegmentDirection;
            double speed = motion.Speed;
            int resolutionOrder = motion.ResolutionOrder;
            GridPosition destination = entity.Position.Offset(direction);
            _world.CommitMoves([new PlannedMove(entity.Id, entity.Position, destination)]);
            events.Add(new CellEntered(entity.Id, destination));
            KinematicsSystem.Stop(entity);

            if (_gravity.EvaluateStationary(_world, entity, _gravityDirection, events))
            {
                return;
            }

            if (CanContinuePush(entity, direction, resolutionOrder))
            {
                KinematicsSystem.Start(
                    entity,
                    MotionCause.PlayerPushed,
                    direction,
                    speed,
                    resolutionOrder);
            }
        }

        private bool CanContinuePush(Entity entity, Direction direction, int resolutionOrder)
        {
            GridPosition destination = entity.Position.Offset(direction);
            if (!_world.Contains(destination))
            {
                return false;
            }

            foreach (Entity occupant in _world.GetOccupants(destination))
            {
                MotionState? occupantMotion = occupant.Motion;
                bool followsEarlierPush = occupantMotion is not null
                    && occupantMotion.Cause == MotionCause.PlayerPushed
                    && occupantMotion.SegmentDirection == direction
                    && occupantMotion.ResolutionOrder < resolutionOrder;
                if (!followsEarlierPush
                    && _interactions.Classify(entity, occupant) != OccupantInteraction.Allow)
                {
                    return false;
                }
            }

            return true;
        }

        private void ResolvePlayerBoundary(
            Entity player,
            MotionState motion,
            List<SimulationEvent> events)
        {
            Direction direction = motion.SegmentDirection;
            KinematicsSystem.Stop(player);
            _playerController!.EndSegment();

            MovementPlan plan = _movement.Plan(new MoveRequest(player.Id, direction));
            if (plan.Result == MoveResult.Blocked)
            {
                events.Add(new PlayerBlocked(player.Id, direction));
                return;
            }

            if (plan.Result == MoveResult.Pushed)
            {
                throw new NotSupportedException("Continuous pushing is not implemented.");
            }

            MovementStep step = plan.Steps.Single();
            _world.CommitMoves([new PlannedMove(step.EntityId, step.From, step.To)]);
            events.Add(new CellEntered(player.Id, step.To));
            _playerController.TryStart(events, emitStoppedWhenNoInput: true);
        }
    }
}
