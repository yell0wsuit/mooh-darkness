using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;

namespace Mooh.Core.Motion
{
    /// <summary>
    /// Starts, advances, and stops continuous entity motion.
    /// </summary>
    public static class KinematicsSystem
    {
        /// <summary>Starts an entity moving from its current cell.</summary>
        /// <param name="entity">The entity to move.</param>
        /// <param name="cause">The behavior responsible for the motion.</param>
        /// <param name="direction">The direction of travel.</param>
        /// <param name="speed">The positive speed in cells per second.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The entity is already moving.</exception>
        public static void Start(Entity entity, MotionCause cause, Direction direction, double speed)
        {
            ArgumentNullException.ThrowIfNull(entity);
            if (entity.Motion is not null)
            {
                throw new InvalidOperationException("An entity already in motion cannot start another motion.");
            }

            entity.Motion = new MotionState(cause, direction, speed);
        }

        /// <summary>Advances an entity's active motion by elapsed time.</summary>
        /// <param name="entity">The moving entity.</param>
        /// <param name="elapsed">The elapsed simulation time.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">The entity is not moving.</exception>
        public static void Advance(Entity entity, TimeSpan elapsed)
        {
            ArgumentNullException.ThrowIfNull(entity);
            MotionState motion = entity.Motion
                ?? throw new InvalidOperationException("An entity without active motion cannot be advanced.");
            motion.Progress += motion.Speed * elapsed.TotalSeconds;
        }

        /// <summary>Stops an entity's active motion.</summary>
        /// <param name="entity">The entity to stop.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
        public static void Stop(Entity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            entity.Motion = null;
        }
    }
}
