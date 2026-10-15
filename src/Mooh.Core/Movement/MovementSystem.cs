using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;

namespace Mooh.Core.Movement
{
    /// <summary>
    /// Plans movement requests without mutating the authoritative world.
    /// </summary>
    /// <remarks>
    /// Initializes a movement system for a world and interaction policy.
    /// </remarks>
    /// <param name="world">The authoritative world to inspect.</param>
    /// <param name="interactions">The policy used to classify destination occupants.</param>
    public sealed class MovementSystem(WorldGrid world, IInteractionPolicy interactions)
    {
        private readonly IInteractionPolicy _interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        private readonly WorldGrid _world = world ?? throw new ArgumentNullException(nameof(world));

        /// <summary>
        /// Plans a movement request without mutating the world.
        /// </summary>
        /// <param name="request">The entity and direction to plan.</param>
        /// <returns>The movement outcome and ordered entity transitions.</returns>
        public MovementPlan Plan(MoveRequest request)
        {
            return Plan(request, gravityDirection: null);
        }

        internal MovementPlan Plan(MoveRequest request, Direction gravityDirection)
        {
            return Plan(request, (Direction?)gravityDirection);
        }

        private MovementPlan Plan(MoveRequest request, Direction? gravityDirection)
        {
            Entity mover = _world.GetEntity(request.EntityId);
            if (mover.Motion is not null
                || mover.Interactions.HasFlag(InteractionMask.MovementLocked))
            {
                return new MovementPlan(MoveResult.Blocked, []);
            }

            GridPosition destination = mover.Position.Offset(request.Direction);
            if (!_world.Contains(destination))
            {
                return new MovementPlan(MoveResult.Blocked, []);
            }

            List<Entity> pushed = [];
            foreach (Entity occupant in _world.GetOccupants(destination))
            {
                switch (_interactions.Classify(mover, occupant))
                {
                    case OccupantInteraction.Allow:
                        break;
                    case OccupantInteraction.Block:
                        return new MovementPlan(MoveResult.Blocked, []);
                    case OccupantInteraction.Push:
                        pushed.Add(occupant);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown occupant interaction.");
                }
            }

            if (pushed.Count == 0)
            {
                return new MovementPlan(
                    MoveResult.Moved,
                    [new MovementStep(mover.Id, mover.Position, destination)]);
            }

            // GamePlayer.CtrlMove gates a gravity-axis push twice. The outer gate needs every
            // occupant to carry CNT_CMaskObjVPush, and the ordinary push branch is then reached
            // through `elif vm and ...`, so it is unavailable along gravity at all. Only an
            // occupant that also carries CNT_CMaskPlCanPushThEat takes the earlier branch and
            // survives both.
            if (gravityDirection is Direction gravity
                && IsOnSameAxis(request.Direction, gravity)
                && (pushed.Any(
                        entity => !entity.Interactions.HasFlag(InteractionMask.GravityAxisPushable))
                    || !pushed.Exists(
                        entity => entity.Interactions.HasFlag(InteractionMask.PushesThroughOccupants))))
            {
                return new MovementPlan(MoveResult.Blocked, []);
            }

            // A push can take over motion along its direction, preserving its offset.
            // Opposite and perpendicular motion still block it.
            if (pushed.Exists(entity => entity.Motion is { } motion
                && motion.TravelDirection != request.Direction))
            {
                return new MovementPlan(MoveResult.Blocked, []);
            }

            EntityId[] offered = [.. pushed.Select(entity => entity.Id)];

            // Both recovered gates on the cell beyond — CellObjCheck(n, 0, CNT_CMaskCallCanPushTh,
            // True) and CellObjCheck(n, 0, 0, True) — are ordinary queries.
            GridPosition pushedDestination = destination.Offset(request.Direction);
            List<Entity> pushedDestinationOccupants = _world.Contains(pushedDestination)
                ? [.. CellQuery.VisibleOccupants(_world, pushedDestination)]
                : [];
            if (!_world.Contains(pushedDestination)
                || (pushedDestinationOccupants.Count != 0
                    && !pushed.Exists(
                        entity => entity.Interactions.HasFlag(InteractionMask.PushesThroughOccupants)))
                || pushedDestinationOccupants.Exists(
                    entity => !entity.Interactions.HasFlag(InteractionMask.CanBePushedThrough))
                || pushed.Exists(
                    entity => entity.Interactions.HasFlag(InteractionMask.MovementLocked)))
            {
                return new MovementPlan(MoveResult.Blocked, [], offered);
            }

            List<MovementStep> steps = [];
            steps.AddRange(
                pushed.Select(
                    entity => new MovementStep(entity.Id, entity.Position,
                        entity.Motion is null ? pushedDestination : entity.Position)));
            steps.Add(new MovementStep(mover.Id, mover.Position, destination));
            return new MovementPlan(MoveResult.Pushed, steps, offered);
        }

        private static bool IsOnSameAxis(Direction first, Direction second)
        {
            return ((int)first & 1) == ((int)second & 1);
        }
    }
}
