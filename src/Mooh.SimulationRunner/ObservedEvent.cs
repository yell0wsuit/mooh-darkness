using Mooh.Core.Combat;
using Mooh.Core.Grid;

namespace Mooh.SimulationRunner
{
    /// <summary>Represents one simulation event normalized to scenario aliases.</summary>
    public sealed record ObservedEvent
    {
        /// <summary>Initializes a normalized simulation event.</summary>
        /// <param name="type">The concrete event type name.</param>
        /// <param name="entity">The primary entity alias.</param>
        /// <param name="eatenEntity">An optional eaten-entity alias.</param>
        /// <param name="direction">An optional direction.</param>
        /// <param name="from">An optional pre-rotation direction.</param>
        /// <param name="to">An optional post-rotation direction.</param>
        /// <param name="position">An optional grid position.</param>
        /// <param name="fromPosition">An optional movement source position.</param>
        /// <param name="toPosition">An optional movement destination position.</param>
        /// <param name="sourceEntity">An optional source-entity alias.</param>
        /// <param name="damageKind">An optional semantic damage kind.</param>
        /// <param name="damageResult">An optional damage-resolution result.</param>
        /// <param name="amount">An optional damage amount.</param>
        /// <param name="remainingHealth">Optional remaining health after damage.</param>
        /// <param name="durationMilliseconds">An optional event duration in milliseconds.</param>
        /// <param name="isSmall">An optional target or resulting monster-form flag.</param>
        /// <param name="worldRotation">An optional world-rotation direction.</param>
        /// <param name="width">An optional world width in cells.</param>
        /// <param name="height">An optional world height in cells.</param>
        public ObservedEvent(
            string type,
            string entity,
            string? eatenEntity = null,
            Direction? direction = null,
            Direction? from = null,
            Direction? to = null,
            GridPosition? position = null,
            GridPosition? fromPosition = null,
            GridPosition? toPosition = null,
            string? sourceEntity = null,
            DamageKind? damageKind = null,
            DamageResult? damageResult = null,
            double? amount = null,
            double? remainingHealth = null,
            long? durationMilliseconds = null,
            bool? isSmall = null,
            WorldRotationDirection? worldRotation = null,
            int? width = null,
            int? height = null)
        {
            Type = type;
            Entity = entity;
            EatenEntity = eatenEntity;
            Direction = direction;
            From = from;
            To = to;
            Position = position;
            FromPosition = fromPosition;
            ToPosition = toPosition;
            SourceEntity = sourceEntity;
            DamageKind = damageKind;
            DamageResult = damageResult;
            Amount = amount;
            RemainingHealth = remainingHealth;
            DurationMilliseconds = durationMilliseconds;
            IsSmall = isSmall;
            WorldRotation = worldRotation;
            Width = width;
            Height = height;
        }

        /// <summary>Gets the concrete event type name.</summary>
        public string Type { get; }

        /// <summary>Gets the primary entity alias.</summary>
        public string Entity { get; }

        /// <summary>Gets an optional eaten-entity alias.</summary>
        public string? EatenEntity { get; }

        /// <summary>Gets an optional direction.</summary>
        public Direction? Direction { get; }

        /// <summary>Gets an optional pre-rotation direction.</summary>
        public Direction? From { get; }

        /// <summary>Gets an optional post-rotation direction.</summary>
        public Direction? To { get; }

        /// <summary>Gets an optional grid position.</summary>
        public GridPosition? Position { get; }

        /// <summary>Gets an optional movement source position.</summary>
        public GridPosition? FromPosition { get; }

        /// <summary>Gets an optional movement destination position.</summary>
        public GridPosition? ToPosition { get; }

        /// <summary>Gets an optional source-entity alias.</summary>
        public string? SourceEntity { get; }

        /// <summary>Gets an optional semantic damage kind.</summary>
        public DamageKind? DamageKind { get; }

        /// <summary>Gets an optional damage-resolution result.</summary>
        public DamageResult? DamageResult { get; }

        /// <summary>Gets an optional damage amount.</summary>
        public double? Amount { get; }

        /// <summary>Gets optional remaining health after damage.</summary>
        public double? RemainingHealth { get; }

        /// <summary>Gets an optional event duration in milliseconds.</summary>
        public long? DurationMilliseconds { get; }

        /// <summary>Gets an optional target or resulting monster-form flag.</summary>
        public bool? IsSmall { get; }

        /// <summary>Gets an optional world-rotation direction.</summary>
        public WorldRotationDirection? WorldRotation { get; }

        /// <summary>Gets an optional world width in cells, reported after a rotation.</summary>
        public int? Width { get; }

        /// <summary>Gets an optional world height in cells, reported after a rotation.</summary>
        public int? Height { get; }
    }
}
