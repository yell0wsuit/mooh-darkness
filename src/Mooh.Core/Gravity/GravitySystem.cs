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
    /// <param name="random">The random generator used to choose between equivalent slide routes.</param>
    public sealed class GravitySystem(IGravityCollisionPolicy collisionPolicy, Random random)
    {
        private readonly IGravityCollisionPolicy _collisionPolicy = collisionPolicy
            ?? throw new ArgumentNullException(nameof(collisionPolicy));
        private readonly Random _random = random ?? throw new ArgumentNullException(nameof(random));

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
                _ = EvaluateStationary(world, entity, gravityDirection, events);
            }
        }

        internal bool EvaluateStationary(
            WorldGrid world,
            Entity entity,
            Direction gravityDirection,
            ICollection<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(events);
            if (entity.GravityBody is not GravityBody body || entity.Motion is not null)
            {
                return false;
            }

            GridPosition destination = entity.Position.Offset(gravityDirection);
            if (_collisionPolicy.CanEnter(world, entity, destination))
            {
                KinematicsSystem.Start(entity, MotionCause.Falling, gravityDirection, body.FallSpeed);
                events.Add(new FallStarted(entity.Id, gravityDirection));
                return true;
            }

            if (!body.CanSlide)
            {
                return false;
            }

            Direction[] candidates = [gravityDirection.TurnLeft(), gravityDirection.TurnRight()];
            List<Direction> viable = [];
            foreach (Direction candidate in candidates)
            {
                GridPosition side = entity.Position.Offset(candidate);
                GridPosition diagonal = side.Offset(gravityDirection);
                if (_collisionPolicy.CanEnter(world, entity, side)
                    && _collisionPolicy.CanEnter(world, entity, diagonal))
                {
                    viable.Add(candidate);
                }
            }

            if (viable.Count == 0)
            {
                return false;
            }

            Direction slideDirection = viable.Count == 1 ? viable[0] : viable[_random.Next(2)];
            KinematicsSystem.Start(entity, MotionCause.Sliding, slideDirection, body.FallSpeed);
            events.Add(new SlideStarted(entity.Id, slideDirection));
            return true;
        }

        internal bool CanEnter(WorldGrid world, Entity entity, GridPosition destination)
        {
            return _collisionPolicy.CanEnter(world, entity, destination);
        }
    }
}
