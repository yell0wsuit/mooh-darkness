using System;

using Mooh.Core.Entities;

namespace Mooh.Core.Combat
{
    /// <summary>Describes one requested health-damage application.</summary>
    public sealed record DamageRequest
    {
        /// <summary>Initializes a damage request.</summary>
        /// <param name="sourceEntityId">The entity responsible for the damage.</param>
        /// <param name="targetEntityId">The intended damaged entity.</param>
        /// <param name="kind">The semantic damage category.</param>
        /// <param name="amount">The finite positive damage amount.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> or <paramref name="amount"/> is invalid.</exception>
        public DamageRequest(
            EntityId sourceEntityId,
            EntityId targetEntityId,
            DamageKind kind,
            double amount)
        {
            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            if (!double.IsFinite(amount) || amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            Kind = kind;
            Amount = amount;
        }

        /// <summary>Gets the entity responsible for the damage.</summary>
        public EntityId SourceEntityId { get; }

        /// <summary>Gets the intended damaged entity.</summary>
        public EntityId TargetEntityId { get; }

        /// <summary>Gets the semantic damage category.</summary>
        public DamageKind Kind { get; }

        /// <summary>Gets the damage amount.</summary>
        public double Amount { get; }
    }
}
