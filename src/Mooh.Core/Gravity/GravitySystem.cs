using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Motion;
using Mooh.Core.Simulation;

namespace Mooh.Core.Gravity
{
    /// <summary>
    /// Evaluates stationary entities for gravity-driven movement.
    /// </summary>
    /// <remarks>Initializes gravity evaluation with the specified collision policy.</remarks>
    /// <param name="collisionPolicy">The policy controlling cell entry.</param>
    public sealed class GravitySystem(IGravityCollisionPolicy collisionPolicy)
    {
        private readonly IGravityCollisionPolicy _collisionPolicy = collisionPolicy
            ?? throw new ArgumentNullException(nameof(collisionPolicy));

        /// <summary>Starts unsupported stationary gravity bodies falling.</summary>
        /// <param name="world">The world whose entities are evaluated.</param>
        /// <param name="gravityDirection">The current direction of gravity.</param>
        /// <param name="events">The event collection receiving start events.</param>
        public void EvaluateStationary(
            WorldGrid world,
            Direction gravityDirection,
            ICollection<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(events);
            foreach (Entity entity in world.Entities)
            {
                if (entity.GravityBody is not GravityBody body || entity.Motion is not null)
                {
                    continue;
                }

                GridPosition destination = entity.Position.Offset(gravityDirection);
                if (!_collisionPolicy.CanEnter(world, entity, destination))
                {
                    continue;
                }

                KinematicsSystem.Start(entity, MotionCause.Falling, gravityDirection, body.FallSpeed);
                events.Add(new FallStarted(entity.Id, gravityDirection));
            }
        }

        internal bool CanEnter(WorldGrid world, Entity entity, GridPosition destination)
        {
            return _collisionPolicy.CanEnter(world, entity, destination);
        }
    }
}
