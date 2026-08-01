using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Simulation;

namespace Mooh.Core.Player
{
    internal sealed class PlayerController(
        Entity player,
        WorldGrid world,
        MovementSystem movement,
        Direction gravityDirection)
    {
        private const double HalfTurnSeconds = 0.210;
        private const double QuarterTurnSeconds = 0.105;
        private const double CounterMotionMultiplier = 0.75;
        private const double TimeTolerance = 1e-12;

        private readonly MovementSystem _movement = movement
            ?? throw new ArgumentNullException(nameof(movement));
        private readonly Entity _player = player
            ?? throw new ArgumentNullException(nameof(player));
        private readonly WorldGrid _world = world
            ?? throw new ArgumentNullException(nameof(world));
        private Direction _gravityDirection = gravityDirection;
        private PlayerInputState _input;
        private Direction? _lastAppliedTravelDirection;

        public void SetInput(PlayerInputState input)
        {
            _input = input;
        }

        public void SetGravity(Direction direction)
        {
            _gravityDirection = direction;
        }

        public void TryStart(
            ICollection<SimulationEvent> events,
            bool emitStoppedWhenNoInput = false)
        {
            ArgumentNullException.ThrowIfNull(events);
            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            if (_player.Motion is not null || body.Rotation is not null)
            {
                return;
            }

            Direction? direction = _input.FirstHeldDirection();
            if (direction is not Direction heldDirection)
            {
                if (emitStoppedWhenNoInput)
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

        private void StartMove(
            Direction direction,
            PlayerBody body,
            ICollection<SimulationEvent> events)
        {

            GridPosition destination = _player.Position.Offset(direction);
            EatDestination(destination, events);
            MovementPlan plan = _movement.Plan(
                new MoveRequest(_player.Id, direction),
                _gravityDirection);
            if (plan.Result == MoveResult.Blocked)
            {
                events.Add(new PlayerBlocked(_player.Id, direction));
                return;
            }

            bool hasCounterMotion = plan.Result == MoveResult.Pushed
                ? HasCounterMotion(destination, direction)
                    || HasCounterMotion(destination.Offset(direction), direction)
                : HasCounterMotion(destination, direction);
            double speedMultiplier = hasCounterMotion ? CounterMotionMultiplier : 1;

            _world.CommitMoves(
                [.. plan.Steps.Select(step => new PlannedMove(step.EntityId, step.From, step.To))]);

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
            ICollection<SimulationEvent> events)
        {
            int resolutionOrder = 0;
            foreach (MovementStep step in plan.Steps)
            {
                if (step.EntityId == _player.Id)
                {
                    continue;
                }

                Entity pushed = _world.GetEntity(step.EntityId);
                KinematicsSystem.Start(
                    pushed,
                    MotionCause.PlayerPushed,
                    direction,
                    playerBody.MoveSpeed * playerBody.PushSpeedMultiplier * speedMultiplier,
                    resolutionOrder);
                resolutionOrder++;
                events.Add(new PushStarted(pushed.Id, direction));
            }
        }

        public void HandleInTransit(ICollection<SimulationEvent> events)
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

        public void AdvanceRotation(double elapsedSeconds, ICollection<SimulationEvent> events)
        {
            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            PlayerRotation? rotation = body.Rotation;
            if (rotation is null)
            {
                return;
            }

            rotation.RemainingSeconds -= elapsedSeconds;
            if (rotation.RemainingSeconds > TimeTolerance)
            {
                return;
            }

            body.Rotation = null;
            events.Add(new PlayerRotationCompleted(_player.Id, rotation.To));
            if (rotation.SuspendedProgress is double suspendedProgress)
            {
                GridPosition destination = _player.Position.Offset(rotation.To);
                _world.CommitMoves([new PlannedMove(_player.Id, _player.Position, destination)]);
                KinematicsSystem.Start(
                    _player,
                    MotionCause.PlayerWalking,
                    rotation.To,
                    body.MoveSpeed);
                _player.Motion!.Progress = 1 - suspendedProgress;
                _lastAppliedTravelDirection = rotation.To;
                events.Add(new PlayerMoveStarted(_player.Id, rotation.To));
                return;
            }

            TryStart(events);
        }

        private void StartRotation(
            PlayerBody body,
            Direction direction,
            double? suspendedProgress,
            ICollection<SimulationEvent> events)
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
        }

        private bool HasCounterMotion(GridPosition position, Direction direction)
        {
            return _world.Contains(position)
                && _world.GetOccupants(position).Any(
                    entity => entity.Motion is not null
                        && entity.Motion.TravelDirection != direction);
        }

        private void EatDestination(
            GridPosition destination,
            ICollection<SimulationEvent> events)
        {
            if (!_world.Contains(destination))
            {
                return;
            }

            foreach (Entity entity in _world.GetOccupants(destination))
            {
                if (!entity.Interactions.HasFlag(InteractionMask.PlayerEdible))
                {
                    continue;
                }

                _ = _world.Remove(entity.Id);
                events.Add(new PlayerAte(_player.Id, entity.Id));
            }
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
