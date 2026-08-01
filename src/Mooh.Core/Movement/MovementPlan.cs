using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Mooh.Core.Entities;

namespace Mooh.Core.Movement
{
    /// <summary>
    /// Describes the non-mutating result of planning a movement request.
    /// </summary>
    /// <param name="result">The overall movement outcome.</param>
    /// <param name="steps">The ordered entity transitions.</param>
    public sealed class MovementPlan(MoveResult result, IReadOnlyList<MovementStep> steps)
    {
        /// <summary>Gets the overall movement outcome.</summary>
        public MoveResult Result { get; } = result;

        /// <summary>Gets the ordered entity transitions.</summary>
        public IReadOnlyList<MovementStep> Steps { get; } = ValidateSteps(steps);

        private static ReadOnlyCollection<MovementStep> ValidateSteps(IReadOnlyList<MovementStep> steps)
        {
            ArgumentNullException.ThrowIfNull(steps);
            HashSet<EntityId> entityIds = [];
            foreach (MovementStep step in steps)
            {
                if (!entityIds.Add(step.EntityId))
                {
                    throw new InvalidOperationException("A movement plan cannot contain one entity more than once.");
                }
            }

            return Array.AsReadOnly([.. steps]);
        }
    }
}
