using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Motion;
using Mooh.Core.Movement;
using Mooh.Core.Simulation;

namespace Mooh.Core.Player
{
    internal sealed class PlayerController(Entity player, MovementSystem movement)
    {
        private readonly MovementSystem _movement = movement
            ?? throw new ArgumentNullException(nameof(movement));
        private readonly Entity _player = player
            ?? throw new ArgumentNullException(nameof(player));
        private PlayerInputState _input;

        public void SetInput(PlayerInputState input)
        {
            _input = input;
        }

        public void TryStart(ICollection<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(events);
            if (_player.Motion is not null)
            {
                return;
            }

            Direction? direction = _input.FirstHeldDirection();
            if (direction is not Direction heldDirection)
            {
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

            if (plan.Result == MoveResult.Pushed)
            {
                throw new NotSupportedException("Continuous pushing is not implemented.");
            }

            KinematicsSystem.Start(
                _player,
                MotionCause.PlayerWalking,
                heldDirection,
                body.MoveSpeed);
            events.Add(new PlayerMoveStarted(_player.Id, heldDirection));
        }
    }
}
