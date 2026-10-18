using System.Collections.Generic;

namespace Mooh.Presentation.Animation
{
    /// <summary>One recovered state map: the page its states draw from and the states themselves.</summary>
    /// <param name="Page">The sprite page most states draw from.</param>
    /// <param name="States">The states by their recovered interned name, such as <c>st_wlkleft</c>.</param>
    public sealed record AnimationTable(string Page, IReadOnlyDictionary<string, AnimationClip> States);
}
