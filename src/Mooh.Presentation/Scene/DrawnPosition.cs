using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Motion;
using Mooh.Core.Player;

namespace Mooh.Presentation.Scene
{
    /// <summary>Places an entity between the cells of its current move.</summary>
    /// <remarks>
    /// An entity commits its destination cell when a move starts, so its
    /// <see cref="Entity.Position"/> is already the cell it is heading for while
    /// <see cref="MotionState.Progress"/> runs from zero to one. The drawn position trails that
    /// cell by the unfinished fraction; a reversal drives progress back toward zero and the same
    /// expression carries the entity back.
    /// <para>
    /// A teleporting player ignores the grid: <c>_TPortCFunc</c> flies it along the straight line
    /// between the two teleporters at a constant speed.
    /// </para>
    /// </remarks>
    public static class DrawnPosition
    {
        /// <summary>Gets an entity's drawn position in cells.</summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The column and row, fractional while the entity moves.</returns>
        public static (double X, double Y) Of(Entity entity)
        {
            System.ArgumentNullException.ThrowIfNull(entity);
            if (entity.PlayerBody is { Action.Kind: PlayerActionKind.TeleportFlight, Teleport: PlayerTeleport flight })
            {
                return (
                    flight.Origin.X + ((flight.Destination.X - flight.Origin.X) * flight.Progress),
                    flight.Origin.Y + ((flight.Destination.Y - flight.Origin.Y) * flight.Progress));
            }

            if (entity.Motion is not MotionState motion)
            {
                return (entity.Position.X, entity.Position.Y);
            }

            double trailing = 1 - motion.Progress;
            (int dx, int dy) = Step(motion.SegmentDirection);
            return (entity.Position.X - (dx * trailing), entity.Position.Y - (dy * trailing));
        }

        /// <summary>Gets the unit cell step of a direction.</summary>
        /// <param name="direction">The direction.</param>
        /// <returns>The column and row change.</returns>
        public static (int X, int Y) Step(Direction direction)
        {
            return direction switch
            {
                Direction.Left => (-1, 0),
                Direction.Up => (0, -1),
                Direction.Right => (1, 0),
                Direction.Down => (0, 1),
                _ => (0, 0),
            };
        }
    }
}
