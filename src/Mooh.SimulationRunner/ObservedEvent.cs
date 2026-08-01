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
        public ObservedEvent(
            string type,
            string entity,
            string? eatenEntity = null,
            Direction? direction = null,
            Direction? from = null,
            Direction? to = null,
            GridPosition? position = null)
        {
            Type = type;
            Entity = entity;
            EatenEntity = eatenEntity;
            Direction = direction;
            From = from;
            To = to;
            Position = position;
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
    }
}
