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
        /// <param name="segmentDirection">The direction from the source cell to the destination cell.</param>
        /// <param name="speed">The positive speed in cells per second.</param>
        /// <param name="resolutionOrder">The order in which simultaneous boundaries are resolved.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="speed"/> is not finite and positive.
        /// </exception>
        public MotionState(
            MotionCause cause,
            Direction segmentDirection,
            double speed,
            int resolutionOrder = 0)
        {
            if (!double.IsFinite(speed) || speed <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }

            Cause = cause;
            SegmentDirection = segmentDirection;
            Speed = speed;
            ResolutionOrder = resolutionOrder;
        }

        /// <summary>Gets the behavior responsible for the motion.</summary>
        public MotionCause Cause { get; }

        /// <summary>Gets the direction from the source cell to the destination cell.</summary>
        public Direction SegmentDirection { get; }

        /// <summary>Gets the sign applied to progress while advancing along the segment.</summary>
        public int TravelSign { get; internal set; } = 1;

        /// <summary>Gets the entity's current direction of travel along the segment.</summary>
        public Direction TravelDirection => TravelSign == 1
            ? SegmentDirection
            : SegmentDirection.Opposite();

        /// <summary>Gets the order in which simultaneous boundaries are resolved.</summary>
        public int ResolutionOrder { get; }

        /// <summary>Gets the speed in cells per second.</summary>
        public double Speed { get; }

        /// <summary>Gets the fractional progress toward the destination cell.</summary>
        public double Progress { get; internal set; }
    }
}
