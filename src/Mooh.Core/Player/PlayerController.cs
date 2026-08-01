using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Simulation;

namespace Mooh.Core.Player
{
    internal sealed class PlayerController(
        Entity player,
        WorldGrid world,
        MovementSystem movement)
    {
        private readonly MovementSystem _movement = movement
            ?? throw new ArgumentNullException(nameof(movement));
        private readonly Entity _player = player
            ?? throw new ArgumentNullException(nameof(player));
        private readonly WorldGrid _world = world
            ?? throw new ArgumentNullException(nameof(world));
        private PlayerInputState _input;
        private Direction? _lastAppliedTravelDirection;

        public void SetInput(PlayerInputState input)
        {
            _input = input;
        }

        public void TryStart(
            ICollection<SimulationEvent> events,
            bool emitStoppedWhenNoInput = false)
        {
            ArgumentNullException.ThrowIfNull(events);
            if (_player.Motion is not null)
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

            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            body.FacingDirection = heldDirection;

            MovementPlan plan = _movement.Plan(new MoveRequest(_player.Id, heldDirection));
            if (plan.Result == MoveResult.Blocked)
            {
                events.Add(new PlayerBlocked(_player.Id, heldDirection));
                return;
            }

            _world.CommitMoves(
                [.. plan.Steps.Select(step => new PlannedMove(step.EntityId, step.From, step.To))]);

            if (plan.Result == MoveResult.Pushed)
            {
                StartPushedMembers(plan, heldDirection, body, events);
            }

            KinematicsSystem.Start(
                _player,
                MotionCause.PlayerWalking,
                heldDirection,
                body.MoveSpeed);
            _lastAppliedTravelDirection = heldDirection;
            events.Add(new PlayerMoveStarted(_player.Id, heldDirection));
        }

        private void StartPushedMembers(
            MovementPlan plan,
            Direction direction,
            PlayerBody playerBody,
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
                    playerBody.MoveSpeed * playerBody.PushSpeedMultiplier,
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

            _lastAppliedTravelDirection = direction;
            if (direction == motion.TravelDirection)
            {
                return;
            }

            GridPosition destination = _player.Position.Offset(direction);
            _world.CommitMoves([new PlannedMove(_player.Id, _player.Position, destination)]);
            KinematicsSystem.Reverse(_player);
            PlayerBody body = _player.PlayerBody
                ?? throw new InvalidOperationException("The player entity has no player body.");
            body.FacingDirection = direction;
            events.Add(new PlayerReversed(_player.Id, direction));
        }

        public void EndSegment()
        {
            _lastAppliedTravelDirection = null;
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
