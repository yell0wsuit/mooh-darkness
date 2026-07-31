using System.Collections.Generic;

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
        public IReadOnlyList<MovementStep> Steps { get; } = steps;
    }
}
