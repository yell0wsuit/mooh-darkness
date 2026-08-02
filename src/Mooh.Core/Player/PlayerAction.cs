using Mooh.Core.Grid;

namespace Mooh.Core.Player
{
    /// <summary>
    /// Tracks the timed uncontrollable action the player started with the fire control. The
    /// recovered states carry no <c>CtrlCellState</c> or <c>CtrlMoveState</c> and no
    /// <c>bCanStrike</c>, so the player can neither walk nor fire again until the action ends.
    /// </summary>
    public sealed class PlayerAction
    {
        internal PlayerAction(PlayerActionKind kind, Direction direction, double remainingSeconds)
        {
            Kind = kind;
            Direction = direction;
            RemainingSeconds = remainingSeconds;
        }

        /// <summary>Gets which action phase is running.</summary>
        public PlayerActionKind Kind { get; }

        /// <summary>Gets the direction the action is applied in.</summary>
        public Direction Direction { get; }

        /// <summary>Gets the remaining duration of the current phase in seconds.</summary>
        public double RemainingSeconds { get; internal set; }
    }
}
