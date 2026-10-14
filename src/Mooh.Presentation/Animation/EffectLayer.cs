using System.Collections.Generic;

namespace Mooh.Presentation.Animation
{
    /// <summary>
    /// One layer of a recovered one-shot effect, a <c>CSfxAnimObj.CType</c>: so many objects, each
    /// playing a random variant once at a random speed, scattered around the effect's cell.
    /// </summary>
    /// <param name="Page">The sprite page.</param>
    /// <param name="Variants">The variants; each object plays one, <c>App.Rand.List</c>.</param>
    /// <param name="FrameMilliseconds">The frame time range; each object's is uniform in speed between them.</param>
    /// <param name="Shift">The range each object's column and row are scattered by, in cells.</param>
    /// <param name="Counts">The object counts to choose from, <c>Num</c>.</param>
    /// <param name="Pass">The render pass, <c>RPass</c>.</param>
    public sealed record EffectLayer(
        string Page,
        IReadOnlyList<IReadOnlyList<string>> Variants,
        (double Min, double Max) FrameMilliseconds,
        (double Min, double Max) Shift,
        IReadOnlyList<int> Counts,
        int Pass);
}
