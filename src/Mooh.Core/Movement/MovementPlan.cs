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
    /// <param name="pushOffered">
    /// The occupants that reached the recovered <c>PlPush</c> dispatch, whether or not the push
    /// then succeeded.
    /// </param>
    public sealed class MovementPlan(
        MoveResult result,
        IReadOnlyList<MovementStep> steps,
        IReadOnlyList<EntityId>? pushOffered = null)
    {
        /// <summary>Gets the overall movement outcome.</summary>
        public MoveResult Result { get; } = result;

        /// <summary>Gets the ordered entity transitions.</summary>
        public IReadOnlyList<MovementStep> Steps { get; } = ValidateSteps(steps);

        /// <summary>
        /// Gets the occupants <c>GamePlayer.CtrlMove</c> offers <c>PlPush</c>. The original
        /// dispatches it once the pushed cell passes its still and gravity-axis gates and before
        /// it tests the cell beyond, so a push that is then blocked still reaches the handler.
        /// </summary>
        public IReadOnlyList<EntityId> PushOffered { get; } = Array.AsReadOnly([.. pushOffered ?? []]);

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
