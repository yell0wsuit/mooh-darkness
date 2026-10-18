namespace Mooh.Presentation.Animation
{
    /// <summary>
    /// A registration's floating line, its <c>GetTxt</c> or <c>KillTxt</c>: a text id, perhaps
    /// formatted with a number and perhaps shown only while the treasure bonus is open.
    /// </summary>
    /// <param name="Id">The text id, such as <c>FObjCoin</c>.</param>
    /// <param name="TimedId">The id a timed level uses instead, such as <c>FObjTime</c>.</param>
    /// <param name="Argument">The fixed number the text is formatted with, such as a box's 5.</param>
    /// <param name="FormatsGain">Whether the text is formatted with the health or time gained, <c>Compl.HEat</c>.</param>
    /// <param name="WhileTreasure">Whether the line shows only while the treasure bonus is open, <c>bGameHaveTreas</c>.</param>
    public sealed record ScoreLine(string Id, string? TimedId = null, int? Argument = null, bool FormatsGain = false, bool WhileTreasure = false);
}
