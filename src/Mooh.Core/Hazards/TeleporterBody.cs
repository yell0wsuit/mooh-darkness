using System;
using System.Collections.Generic;

using Mooh.Core.Grid;

namespace Mooh.Core.Hazards
{
    /// <summary>Holds a teleporter's linked destinations and hands them out in turn.</summary>
    /// <remarks>
    /// <para>
    /// The recovered <c>CGameObjTPort.PlCtrl</c> sends the player to <c>LinkCoords[0]</c> and then
    /// rotates the list by one, so a teleporter with several links cycles through them on
    /// successive uses rather than picking one at random. A teleporter with no links at all sends
    /// the player to its own cell, which is the original's way of spelling "go nowhere" and still
    /// costs the player both animations.
    /// </para>
    /// <para>
    /// The links come from the level itself: <c>CLinkCoordBase.__getstate__</c> stores them, and
    /// the archival JSON preserves them under the <c>GameLinkBase.CLinkCoordBase</c> saved-state
    /// class. Every placed teleporter in the corpus carries between zero and six.
    /// </para>
    /// </remarks>
    public sealed class TeleporterBody
    {
        private readonly List<GridPosition> _links;

        /// <summary>Initializes a teleporter with its linked destinations, in level order.</summary>
        /// <param name="links">The linked destination cells, possibly empty.</param>
        /// <exception cref="ArgumentNullException"><paramref name="links"/> is <see langword="null"/>.</exception>
        public TeleporterBody(IEnumerable<GridPosition> links)
        {
            ArgumentNullException.ThrowIfNull(links);
            _links = [.. links];
        }

        /// <summary>Gets the linked destinations in their current rotation order.</summary>
        public IReadOnlyList<GridPosition> Links => _links;

        /// <summary>
        /// Takes the next destination and rotates the list, applying the recovered
        /// <c>LinkCoords = LinkCoords[1:] + LinkCoords[:1]</c>.
        /// </summary>
        /// <param name="ownPosition">The teleporter's own cell, used when it has no links.</param>
        /// <returns>The cell the player is sent to.</returns>
        public GridPosition TakeNextDestination(GridPosition ownPosition)
        {
            if (_links.Count == 0)
            {
                return ownPosition;
            }

            GridPosition destination = _links[0];
            _links.RemoveAt(0);
            _links.Add(destination);
            return destination;
        }
    }
}
