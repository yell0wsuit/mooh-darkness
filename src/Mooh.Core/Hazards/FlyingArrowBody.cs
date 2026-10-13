using System;

using Mooh.Core.Grid;

namespace Mooh.Core.Hazards
{
    /// <summary>
    /// Describes a directional arrow that turns into a projectile when it is triggered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The recovered <c>OArrow*</c> types are ordinary <c>CGameObjFallBase</c> objects that name
    /// one handler, <c>_DoFly_&lt;dir&gt;</c>, for five separate events: <c>OnLand</c>,
    /// <c>OnShake</c>, <c>OnShakeHard</c>, <c>OnExpl</c> and <c>PlCtrl</c>. Whatever triggers it,
    /// the arrow always flies in the fixed direction its type encodes, never in the direction of
    /// the trigger.
    /// </para>
    /// <para>
    /// Launching runs <c>_DoFlyAll</c>, which stops the arrow participating in gravity at all —
    /// the original clears its <c>WEvOnFall</c> and <c>WEvOnSlide</c> registrations — and enters
    /// <c>ST_Work</c>. Flight starts when that animation ends.
    /// </para>
    /// </remarks>
    /// <param name="direction">The fixed direction the arrow flies in.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is undefined.</exception>
    public sealed class FlyingArrowBody(Direction direction)
    {
        /// <summary>
        /// Gets the direction the arrow flies in, the recovered <c>MoveDirGet()</c>. Launching sets
        /// it to the arrow's own direction; a walking push after the launch overwrites it, because
        /// <c>ObjMoveDir</c> sets the move direction and <c>_DoFlyInt</c> flies along it.
        /// </summary>
        public Direction Direction
        {
            get; internal set;
        } = Enum.IsDefined(direction)
            ? direction
            : throw new ArgumentOutOfRangeException(nameof(direction), direction, null);

        /// <summary>
        /// Gets the remaining <c>ST_Work</c> windup, or <see langword="null"/> when the arrow is
        /// not winding up. The recovered state runs seven frames at 35 ms.
        /// </summary>
        public double? WindupRemainingSeconds
        {
            get; internal set;
        }

        /// <summary>Gets whether the arrow has been triggered, whether or not it is flying yet.</summary>
        public bool IsLaunched
        {
            get; internal set;
        }

        /// <summary>
        /// Gets whether the windup has ended, after which every cell arrival runs the flight
        /// decision. A windup ending while the arrow is being pushed defers the first decision to
        /// the push's arrival, the recovered <c>_DlFlyBeg</c> registering <c>_DoFlyInt</c> as the
        /// arrival handler.
        /// </summary>
        public bool IsFlying
        {
            get; internal set;
        }
    }
}
