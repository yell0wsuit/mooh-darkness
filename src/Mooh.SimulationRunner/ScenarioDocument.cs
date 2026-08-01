using System.Collections.Generic;

namespace Mooh.SimulationRunner
{
    /// <summary>Describes a self-verifying deterministic simulation scenario.</summary>
    public sealed class ScenarioDocument
    {
        /// <summary>Gets the scenario schema version.</summary>
        public required int FormatVersion { get; init; }

        /// <summary>Gets the human-readable scenario name.</summary>
        public required string Name { get; init; }

        /// <summary>Gets the initial runtime world definition.</summary>
        public required ScenarioWorld World { get; init; }

        /// <summary>Gets the ordered simulation steps.</summary>
        public required List<ScenarioStep> Steps { get; init; }

        /// <summary>Gets the expected state after all steps complete.</summary>
        public required ExpectedFinalState ExpectFinal { get; init; }
    }

    /// <summary>Describes the initial world and ordered entity registration.</summary>
    public sealed class ScenarioWorld
    {
        /// <summary>Gets the world width in cells.</summary>
        public required int Width { get; init; }

        /// <summary>Gets the world height in cells.</summary>
        public required int Height { get; init; }

        /// <summary>Gets the cardinal gravity direction name.</summary>
        public required string Gravity { get; init; }

        /// <summary>Gets the deterministic random seed.</summary>
        public required int RandomSeed { get; init; }

        /// <summary>Gets entities in registration order.</summary>
        public required List<ScenarioEntity> Entities { get; init; }
    }

    /// <summary>Identifies a grid position in a scenario document.</summary>
    public sealed class ScenarioPosition
    {
        /// <summary>Gets the horizontal cell coordinate.</summary>
        public required int X { get; init; }

        /// <summary>Gets the vertical cell coordinate.</summary>
        public required int Y { get; init; }
    }

    /// <summary>Describes one entity in the initial world.</summary>
    public sealed class ScenarioEntity
    {
        /// <summary>Gets the stable scenario-local entity alias.</summary>
        public required string Alias { get; init; }

        /// <summary>Gets the entity's initial authoritative position.</summary>
        public required ScenarioPosition Position { get; init; }

        /// <summary>Gets the interaction capability names.</summary>
        public required List<string> Interactions { get; init; }

        /// <summary>Gets optional gravity behavior.</summary>
        public ScenarioGravityBody? GravityBody { get; init; }

        /// <summary>Gets optional player behavior.</summary>
        public ScenarioPlayerBody? PlayerBody { get; init; }
    }

    /// <summary>Describes gravity behavior for a scenario entity.</summary>
    public sealed class ScenarioGravityBody
    {
        /// <summary>Gets the fall speed in cells per second.</summary>
        public required double FallSpeed { get; init; }

        /// <summary>Gets whether the body may slide around round support.</summary>
        public required bool CanSlide { get; init; }
    }

    /// <summary>Describes player behavior for a scenario entity.</summary>
    public sealed class ScenarioPlayerBody
    {
        /// <summary>Gets the walking speed in cells per second.</summary>
        public required double MoveSpeed { get; init; }

        /// <summary>Gets the speed multiplier applied to pushed objects.</summary>
        public required double PushSpeedMultiplier { get; init; }

        /// <summary>Gets the initial facing direction name.</summary>
        public required string FacingDirection { get; init; }
    }

    /// <summary>Describes one timed input and advancement boundary.</summary>
    public sealed class ScenarioStep
    {
        /// <summary>Gets held cardinal input names, or an empty list when no input is held.</summary>
        public List<string> Input { get; init; } = [];

        /// <summary>Gets the positive elapsed simulation duration in milliseconds.</summary>
        public required long AdvanceMilliseconds { get; init; }

        /// <summary>Gets the complete ordered event sequence expected from this step.</summary>
        public required List<ExpectedEvent> ExpectEvents { get; init; }
    }

    /// <summary>Describes one expected normalized simulation event.</summary>
    public sealed class ExpectedEvent
    {
        /// <summary>Gets the concrete simulation event type name.</summary>
        public required string Type { get; init; }

        /// <summary>Gets the primary entity alias.</summary>
        public required string Entity { get; init; }

        /// <summary>Gets an optional secondary eaten-entity alias.</summary>
        public string? EatenEntity { get; init; }

        /// <summary>Gets an optional direction name.</summary>
        public string? Direction { get; init; }

        /// <summary>Gets an optional pre-rotation direction name.</summary>
        public string? From { get; init; }

        /// <summary>Gets an optional post-rotation direction name.</summary>
        public string? To { get; init; }

        /// <summary>Gets an optional event position.</summary>
        public ScenarioPosition? Position { get; init; }
    }

    /// <summary>Describes the expected state after all simulation steps.</summary>
    public sealed class ExpectedFinalState
    {
        /// <summary>Gets the exact ordered surviving entity roster.</summary>
        public required List<ExpectedEntityState> Entities { get; init; }
    }

    /// <summary>Describes the expected observable state of one surviving entity.</summary>
    public sealed class ExpectedEntityState
    {
        /// <summary>Gets the entity alias.</summary>
        public required string Alias { get; init; }

        /// <summary>Gets the expected authoritative position.</summary>
        public required ScenarioPosition Position { get; init; }

        /// <summary>Gets the expected active motion, if any.</summary>
        public ExpectedMotionState? Motion { get; init; }

        /// <summary>Gets the expected player-facing direction name, if applicable.</summary>
        public string? FacingDirection { get; init; }

        /// <summary>Gets the expected active player rotation, if any.</summary>
        public ExpectedRotationState? Rotation { get; init; }
    }

    /// <summary>Describes expected observable continuous motion.</summary>
    public sealed class ExpectedMotionState
    {
        /// <summary>Gets the motion cause name.</summary>
        public required string Cause { get; init; }

        /// <summary>Gets the segment direction name.</summary>
        public required string SegmentDirection { get; init; }

        /// <summary>Gets the signed travel orientation along the segment.</summary>
        public required int TravelSign { get; init; }

        /// <summary>Gets the movement speed in cells per second.</summary>
        public required double Speed { get; init; }

        /// <summary>Gets fractional progress toward the segment destination.</summary>
        public required double Progress { get; init; }

        /// <summary>Gets the simultaneous-boundary resolution order.</summary>
        public required int ResolutionOrder { get; init; }
    }

    /// <summary>Describes an expected active player rotation.</summary>
    public sealed class ExpectedRotationState
    {
        /// <summary>Gets the facing direction before rotation.</summary>
        public required string From { get; init; }

        /// <summary>Gets the facing direction after rotation.</summary>
        public required string To { get; init; }

        /// <summary>Gets the remaining rotation duration in seconds.</summary>
        public required double RemainingSeconds { get; init; }

        /// <summary>Gets suspended movement progress, if rotation interrupted transit.</summary>
        public double? SuspendedProgress { get; init; }
    }
}
