using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Hazards
{
    /// <summary>
    /// Describes an object that turns the whole level through a quarter turn when it is struck.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The recovered <c>OWRotL</c> and <c>OWRotR</c> are ordinary <c>CGameObjFallBase</c> types
    /// carrying <c>CNT_CMaskRoundCap | CNT_CMaskCellLockMotion</c> and <c>bFly=True</c>, so they
    /// block movement, support a falling object and never fall themselves. Their one handler is
    /// <c>PlCtrl=GoTo_Work</c>, whose <c>ST_Work</c> animation ends in
    /// <c>GMod.WorldRotStart</c> before the object returns to <c>ST_Stay</c> and becomes usable
    /// again.
    /// </para>
    /// <para>
    /// <c>RPass</c> is zero, and <c>GamePlayer.DoCtrl</c> only dispatches <c>PlCtrl</c> to
    /// occupants of the player's own cell whose <c>RPass</c> is negative, so the fire control
    /// never reaches a rotator directly. The one path in is <c>_AEnd_Str</c>, the hand strike,
    /// which offers <c>PlCtrl</c> to the struck cell before it offers a push — the same path that
    /// launches a flying arrow.
    /// </para>
    /// </remarks>
    /// <param name="direction">The direction this rotator turns the world.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is undefined.</exception>
    public sealed class WorldRotationBody(WorldRotationDirection direction)
    {
        /// <summary>Gets the direction this rotator turns the world.</summary>
        public WorldRotationDirection Direction { get; } = Enum.IsDefined(direction)
            ? direction
            : throw new ArgumentOutOfRangeException(nameof(direction), direction, null);

        /// <summary>
        /// Gets the remaining <c>ST_Work</c> windup, or <see langword="null"/> when the rotator is
        /// idle. The recovered state runs twenty frames at 50 ms; the world only starts turning
        /// once it ends.
        /// </summary>
        public double? WindupRemainingSeconds { get; internal set; }
    }
}
