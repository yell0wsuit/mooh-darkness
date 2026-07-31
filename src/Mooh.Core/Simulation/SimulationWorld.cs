using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Motion;
using Mooh.Core.Movement;

namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Coordinates continuous gravity evaluation and kinematic advancement for a world.
    /// </summary>
    public sealed class SimulationWorld
    {
        private const double BoundaryTolerance = 1e-12;

        private readonly GravitySystem _gravity;
        private readonly double _maximumTravelPerSubstep;
        private readonly WorldGrid _world;
        private readonly Direction _gravityDirection;

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

            List<SimulationEvent> events = [];
            _gravity.EvaluateStationary(_world, _gravityDirection, events);

            double remainingSeconds = elapsed.TotalSeconds;
            while (remainingSeconds > 0)
            {
                Entity[] moving = [.. _world.Entities.Where(entity => entity.Motion is not null)];
                if (moving.Length == 0)
                {
                    break;
                }

                double maximumSpeed = moving.Max(entity => entity.Motion!.Speed);
                double boundarySeconds = moving.Min(
                    entity => (1 - entity.Motion!.Progress) / entity.Motion.Speed);
                double substepSeconds = Math.Min(
                    remainingSeconds,
                    Math.Min(_maximumTravelPerSubstep / maximumSpeed, boundarySeconds));

                TimeSpan substep = TimeSpan.FromSeconds(substepSeconds);
                foreach (Entity entity in moving)
                {
                    KinematicsSystem.Advance(entity, substep);
                }

                remainingSeconds -= substepSeconds;
                if (remainingSeconds < BoundaryTolerance)
                {
                    remainingSeconds = 0;
                }

                ResolveBoundaries(events);
            }

            return events;
        }

        private void ResolveBoundaries(List<SimulationEvent> events)
        {
            bool resolvedAny = false;
            foreach (Entity entity in _world.Entities)
            {
                MotionState? motion = entity.Motion;
                if (motion is null || motion.Progress < 1 - BoundaryTolerance)
                {
                    continue;
                }

                resolvedAny = true;
                GridPosition destination = entity.Position.Offset(motion.Direction);
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

            if (resolvedAny)
            {
                _gravity.EvaluateStationary(_world, _gravityDirection, events);
            }
        }
    }
}
