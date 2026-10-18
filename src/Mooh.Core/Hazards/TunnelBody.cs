using System;

namespace Mooh.Core.Hazards
{
    /// <summary>
    /// Marks a cell as a tunnel the player can travel through, carrying its routing tables.
    /// </summary>
    /// <remarks>
    /// The recovered <c>GameCellPipes.CGameCellPipes</c> is a cell class rather than an object
    /// behavior, which is why the visually similar <c>Pipe*</c> family — plain
    /// <c>GameCells.CGameCellBase</c> cells — carries nothing of the kind and is simply solid.
    /// </remarks>
    /// <param name="shape">The tunnel's recovered routing tables.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shape"/> is <see langword="null"/>.</exception>
    public sealed class TunnelBody(TunnelShape shape)
    {
        /// <summary>Gets the tunnel's recovered routing tables.</summary>
        public TunnelShape Shape { get; } = shape ?? throw new ArgumentNullException(nameof(shape));
    }
}
