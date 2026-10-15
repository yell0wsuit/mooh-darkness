using System.Collections.Generic;

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
    /// <param name="RandomStartPhase">
    /// Whether an object starts its first animation at a random frame, as every
    /// <c>CGameObjFallBase</c> does in its constructor.
    /// </param>
    /// <param name="Glow">
    /// The glow drawn over the object at the game's clock, <c>GlowAnm</c>, or
    /// <see langword="null"/>.
    /// </param>
    /// <param name="Overlay">
    /// The animation drawn over the object at its own frame's phase, <c>OverAnm</c>, or
    /// <see langword="null"/>.
    /// </param>
    public sealed record ObjectLook(
        BackdropStyle? Backdrop,
        (double Min, double Max)? DepthRange,
        bool ReceivesShadow = false,
        bool RandomStartPhase = false,
        LayerAnimation? Glow = null,
        LayerAnimation? Overlay = null)
    {
        /// <summary>
        /// Gets the registration's own sound aliases by field: <c>PlStepSnd</c>, <c>SndLand</c>,
        /// <c>SndKill</c>, <c>SndEat</c>, <c>SndMove</c> and <c>SndExpl</c>, where it names one.
        /// </summary>
        public IReadOnlyDictionary<string, string> Sounds { get; init; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets whether particles bounce off the object's cell, <c>CNT_CMaskPartsCollide</c>
        /// (<c>1 &lt;&lt; 16</c>).
        /// </summary>
        public bool StopsParticles
        {
            get; init;
        }

        /// <summary>Gets the pieces a blast breaks the object into, its <c>EParts</c>.</summary>
        public IReadOnlyList<ParticleBurst> BlastBursts { get; init; } = [];

        /// <summary>Gets the burst of a kill other than by eating, its <c>PrtKill</c>.</summary>
        public IReadOnlyList<ParticleBurst> KillBursts { get; init; } = [];

        /// <summary>Gets the puff the object raises on landing or stopping, its <c>PrtLand</c>, when it names one.</summary>
        public ParticlePuff? LandPuff
        {
            get; init;
        }

        /// <summary>Gets the puff the object raises when pushed, its <c>PrtMove</c>, when it names one.</summary>
        public ParticlePuff? MovePuff
        {
            get; init;
        }
    }
}
