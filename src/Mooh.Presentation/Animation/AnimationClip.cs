using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Grid;

namespace Mooh.Presentation.Animation
{
    /// <summary>
    /// One recovered animation state: its frames, how fast they play and what follows.
    /// </summary>
    /// <param name="Page">The state's own sprite page, or <see langword="null"/> for the table's page.</param>
    /// <param name="Variants">
    /// The frame lists. An entity plays one of them, picked by its editor index, its colour or its
    /// direction exactly as the original's <c>AInds[EInd % len]</c>-style selection does.
    /// </param>
    /// <param name="FlyVariants">
    /// The player's frames for a cell with nothing beneath it, the recovered <c>FList</c>, or
    /// <see langword="null"/>.
    /// </param>
    /// <param name="FrameMilliseconds">
    /// The time per frame. More than one value is the recovered random choice made when the state
    /// is entered; zero means the state never advances.
    /// </param>
    /// <param name="Next">The state the recovered <c>AEnd</c> names, or <see langword="null"/>.</param>
    /// <param name="EndHandler">
    /// The handler the recovered <c>AEnd</c> calls instead of naming a state, such as
    /// <c>_AEnd_Rot</c>, or <see langword="null"/>.
    /// </param>
    /// <param name="Direction">The direction the state faces, the recovered <c>ulDDir</c>.</param>
    /// <param name="Shadow">
    /// The shadow frames, the recovered <c>DInds</c>, such as the game module's <c>SfxShMed</c>, or
    /// <see langword="null"/> for a state that casts none.
    /// </param>
    /// <param name="ShadowPage">The page the shadow frames are on.</param>
    /// <param name="Sound">The sound alias the state plays when it is set, its <c>Snd</c>.</param>
    /// <param name="SoundHandler">
    /// The handler that picks the state's sound instead, such as the player's <c>SndStep1</c>.
    /// </param>
    public sealed record AnimationClip(
        string? Page,
        IReadOnlyList<IReadOnlyList<string>> Variants,
        IReadOnlyList<IReadOnlyList<string>>? FlyVariants,
        IReadOnlyList<int> FrameMilliseconds,
        string? Next,
        string? EndHandler,
        Direction? Direction,
        IReadOnlyList<string>? Shadow = null,
        string? ShadowPage = null,
        string? Sound = null,
        string? SoundHandler = null)
    {
        /// <summary>Gets whether the state never advances past its first frame.</summary>
        public bool IsStill => FrameMilliseconds.All(milliseconds => milliseconds <= 0);
    }
}
