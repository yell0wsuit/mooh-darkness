using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Motion
{
    /// <summary>
    /// Tracks an entity's continuous transit toward an adjacent cell.
    /// </summary>
    public sealed class MotionState
    {
        /// <summary>
        /// Initializes motion from the entity's current cell.
        /// </summary>
        /// <param name="cause">The behavior responsible for the motion.</param>
        /// <param name="direction">The direction of travel.</param>
        /// <param name="speed">The positive speed in cells per second.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="speed"/> is not finite and positive.
        /// </exception>
        public MotionState(MotionCause cause, Direction direction, double speed)
        {
            if (!double.IsFinite(speed) || speed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }

            Cause = cause;
            Direction = direction;
            Speed = speed;
        }

        /// <summary>Gets the behavior responsible for the motion.</summary>
        public MotionCause Cause { get; }

        /// <summary>Gets the direction of travel.</summary>
        public Direction Direction { get; }

        /// <summary>Gets the speed in cells per second.</summary>
        public double Speed { get; }

        /// <summary>Gets the fractional progress toward the destination cell.</summary>
        public double Progress { get; internal set; }
    }
}
