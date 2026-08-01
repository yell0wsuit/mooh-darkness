using Mooh.Core.Grid;
using Mooh.Core.Motion;

namespace Mooh.SimulationRunner
{
    /// <summary>Represents observable continuous motion at scenario completion.</summary>
    /// <param name="Cause">The behavior responsible for motion.</param>
    /// <param name="SegmentDirection">The authoritative movement segment direction.</param>
    /// <param name="TravelSign">The signed orientation along the segment.</param>
    /// <param name="Speed">The speed in cells per second.</param>
    /// <param name="Progress">The fractional segment progress.</param>
    /// <param name="ResolutionOrder">The simultaneous-boundary resolution order.</param>
    public sealed record ObservedMotionState(
        MotionCause Cause,
        Direction SegmentDirection,
        int TravelSign,
        double Speed,
        double Progress,
        int ResolutionOrder);

    /// <summary>Represents an observable active player rotation.</summary>
    /// <param name="From">The facing direction before rotation.</param>
    /// <param name="To">The facing direction after rotation.</param>
    /// <param name="RemainingSeconds">The remaining rotation duration.</param>
    /// <param name="SuspendedProgress">Suspended transit progress, if any.</param>
    public sealed record ObservedRotationState(
        Direction From,
        Direction To,
        double RemainingSeconds,
        double? SuspendedProgress);

    /// <summary>Represents one surviving entity's observable final state.</summary>
    /// <param name="Alias">The stable scenario-local alias.</param>
    /// <param name="Position">The authoritative grid position.</param>
    /// <param name="Motion">Active continuous motion, if any.</param>
    /// <param name="FacingDirection">Player-facing direction, if applicable.</param>
    /// <param name="Rotation">Active player rotation, if any.</param>
    public sealed record ObservedEntityState(
        string Alias,
        GridPosition Position,
        ObservedMotionState? Motion,
        Direction? FacingDirection,
        ObservedRotationState? Rotation);
}
