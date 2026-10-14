namespace Mooh.Presentation.Animation
{
    /// <summary>What an archival registration, <c>CObjRepInfo</c>, says about drawing its objects.</summary>
    /// <param name="Backdrop">The backdrop the type votes for in <c>LvlBackType</c>, its <c>BackType</c>.</param>
    /// <param name="DepthRange">
    /// The range of the random depth shift, <c>fShZ</c>, each object draws with; terrain's is
    /// <c>CellSh</c>, -0.05 to 0.05.
    /// </param>
    /// <param name="ReceivesShadow">
    /// Whether the type's mask carries <c>CNT_CMaskResRShadow</c>, so objects above it cast their
    /// shadows onto it: terrain, rocks and key doors.
    /// </param>
    public sealed record ObjectLook(BackdropStyle? Backdrop, (double Min, double Max)? DepthRange, bool ReceivesShadow = false);
}
