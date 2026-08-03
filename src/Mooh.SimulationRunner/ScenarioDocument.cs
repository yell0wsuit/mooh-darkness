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

        /// <summary>Gets optional health behavior.</summary>
        public ScenarioHealthBody? HealthBody { get; init; }

        /// <summary>Gets optional green-monster behavior.</summary>
        public ScenarioGreenMonsterBody? GreenMonsterBody { get; init; }

        /// <summary>Gets optional blue-monster behavior.</summary>
        public ScenarioBlueMonsterBody? BlueMonsterBody { get; init; }

        /// <summary>Gets optional explosion behavior.</summary>
        public ScenarioExplosionBody? ExplosionBody { get; init; }

        /// <summary>Gets optional world-rotation behavior.</summary>
        public ScenarioWorldRotationBody? WorldRotationBody { get; init; }
    }

    /// <summary>Describes world-rotation behavior for a scenario entity.</summary>
    public sealed class ScenarioWorldRotationBody
    {
        /// <summary>Gets the exact <c>WorldRotationDirection</c> name the rotator turns the world.</summary>
        public required string Direction { get; init; }
    }

    /// <summary>Describes gravity behavior for a scenario entity.</summary>
    public sealed class ScenarioGravityBody
    {
        /// <summary>Gets the fall speed in cells per second.</summary>
        public required double FallSpeed { get; init; }

        /// <summary>Gets whether the body may slide around round support.</summary>
        public required bool CanSlide { get; init; }
    }

    /// <summary>Describes explosion behavior for a scenario entity.</summary>
    public sealed class ScenarioExplosionBody
    {
        /// <summary>Gets the exact explosion-reaction name.</summary>
        public required string Reaction { get; init; }

        /// <summary>Gets whether a completed fall starts a 3x3 explosion.</summary>
        public required bool DetonatesOnLanding { get; init; }
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

    /// <summary>Describes health behavior for a scenario entity.</summary>
    public sealed class ScenarioHealthBody
    {
        /// <summary>Gets the maximum health.</summary>
        public required double Maximum { get; init; }

        /// <summary>Gets the initial current health.</summary>
        public required double Current { get; init; }

        /// <summary>Gets the post-damage immunity duration in milliseconds.</summary>
        public required long ImmunityMilliseconds { get; init; }
    }

    /// <summary>Describes autonomous green-monster behavior for a scenario entity.</summary>
    public sealed class ScenarioGreenMonsterBody
    {
        /// <summary>Gets the walking speed in cells per second.</summary>
        public required double MoveSpeed { get; init; }

        /// <summary>Gets the initial facing direction name.</summary>
        public required string FacingDirection { get; init; }

        /// <summary>Gets the clockwise turn duration in milliseconds.</summary>
        public required long TurnMilliseconds { get; init; }

        /// <summary>Gets the attack wind-up duration in milliseconds.</summary>
        public required long AttackWindupMilliseconds { get; init; }

        /// <summary>Gets the attack recovery duration in milliseconds.</summary>
        public required long AttackRecoveryMilliseconds { get; init; }

        /// <summary>Gets the damage dealt by one strike.</summary>
        public required double StrikeDamage { get; init; }
    }

    /// <summary>Describes autonomous blue-monster behavior for a scenario entity.</summary>
    public sealed class ScenarioBlueMonsterBody
    {
        /// <summary>Gets the walking speed in cells per second.</summary>
        public required double MoveSpeed { get; init; }

        /// <summary>Gets the initial facing direction name.</summary>
        public required string FacingDirection { get; init; }

        /// <summary>Gets the quarter-turn duration in milliseconds.</summary>
        public required long TurnMilliseconds { get; init; }

        /// <summary>Gets the attack wind-up duration in milliseconds.</summary>
        public required long AttackWindupMilliseconds { get; init; }

        /// <summary>Gets the left-facing attack recovery duration in milliseconds.</summary>
        public required long LeftAttackRecoveryMilliseconds { get; init; }

        /// <summary>Gets the non-left attack recovery duration in milliseconds.</summary>
        public required long AttackRecoveryMilliseconds { get; init; }

        /// <summary>Gets the damage dealt by one strike.</summary>
        public required double StrikeDamage { get; init; }
    }

    /// <summary>Describes one timed input and advancement boundary.</summary>
    public sealed class ScenarioStep
    {
        /// <summary>Gets held cardinal input names, or an empty list when no input is held.</summary>
        public List<string>? Input { get; init; }

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

        /// <summary>Gets an optional movement source position.</summary>
        public ScenarioPosition? FromPosition { get; init; }

        /// <summary>Gets an optional movement destination position.</summary>
        public ScenarioPosition? ToPosition { get; init; }

        /// <summary>Gets an optional source-entity alias.</summary>
        public string? SourceEntity { get; init; }

        /// <summary>Gets an optional semantic damage kind.</summary>
        public string? DamageKind { get; init; }

        /// <summary>Gets an optional damage-resolution result.</summary>
        public string? DamageResult { get; init; }

        /// <summary>Gets an optional damage amount.</summary>
        public double? Amount { get; init; }

        /// <summary>Gets optional remaining health after damage.</summary>
        public double? RemainingHealth { get; init; }

        /// <summary>Gets an optional event duration in milliseconds.</summary>
        public long? DurationMilliseconds { get; init; }

        /// <summary>Gets an optional target or resulting monster-form flag.</summary>
        public bool? IsSmall { get; init; }

        /// <summary>Gets an optional exact <c>WorldRotationDirection</c> name.</summary>
        public string? WorldRotation { get; init; }

        /// <summary>Gets an optional world width in cells, reported after a rotation.</summary>
        public int? Width { get; init; }

        /// <summary>Gets an optional world height in cells, reported after a rotation.</summary>
        public int? Height { get; init; }
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

        /// <summary>Gets expected current health, if applicable.</summary>
        public double? CurrentHealth { get; init; }

        /// <summary>Gets expected remaining damage immunity in milliseconds, if applicable.</summary>
        public long? ImmunityMilliseconds { get; init; }

        /// <summary>Gets the expected player reaction name, if applicable.</summary>
        public string? PlayerReaction { get; init; }

        /// <summary>Gets expected remaining player-reaction time in milliseconds, if applicable.</summary>
        public long? ReactionMilliseconds { get; init; }

        /// <summary>Gets the expected green-monster state name, if applicable.</summary>
        public string? GreenMonsterState { get; init; }

        /// <summary>Gets the expected green-monster facing direction, if applicable.</summary>
        public string? GreenMonsterFacingDirection { get; init; }

        /// <summary>Gets expected remaining green-monster state time in milliseconds, if applicable.</summary>
        public long? GreenMonsterRemainingMilliseconds { get; init; }

        /// <summary>Gets whether the Green Monster should be frozen.</summary>
        public bool? GreenMonsterIsFrozen { get; init; }

        /// <summary>Gets whether the Green Monster should use its glasses form.</summary>
        public bool? GreenMonsterIsSmall { get; init; }

        /// <summary>Gets the expected blue-monster state name, if applicable.</summary>
        public string? BlueMonsterState { get; init; }

        /// <summary>Gets the expected blue-monster facing direction, if applicable.</summary>
        public string? BlueMonsterFacingDirection { get; init; }

        /// <summary>Gets expected remaining blue-monster state time in milliseconds, if applicable.</summary>
        public long? BlueMonsterRemainingMilliseconds { get; init; }
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
