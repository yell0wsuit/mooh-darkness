using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Shakes;

namespace Mooh.Core.Levels
{
    /// <summary>Defines the explicit runtime meaning of one archival object type.</summary>
    /// <param name="ArchivalType">The exact case-sensitive archival type name.</param>
    /// <param name="Kind">The semantic runtime family.</param>
    /// <param name="Interactions">The movement interaction capabilities.</param>
    /// <param name="HasGravity">Whether the object participates in directional gravity.</param>
    /// <param name="FallSpeedMultiplier">The multiplier applied to the normal fall speed.</param>
    /// <param name="Variant">The family-specific variant or cardinal direction.</param>
    /// <param name="ImpactStrength">The shake strength produced by a falling landing.</param>
    /// <param name="ExplosionReaction">The reaction when reached by an explosion, if any.</param>
    /// <param name="DetonatesOnLanding">Whether a completed fall starts a 3x3 explosion.</param>
    /// <param name="ShakeReaction">The reaction to an ordinary shake of the object's cell.</param>
    /// <param name="HardShakeReaction">The reaction to a hard shake of the object's cell.</param>
    /// <param name="FireContact">The object's role in the fire-contact rule, if it has one.</param>
    /// <param name="TreatsShadowAsSupport">
    /// Whether a shadow occupant supports the object while it falls, the recovered
    /// <c>bSecondSlideMask</c> flag.
    /// </param>
    public sealed record ArchivalObjectDescriptor(
        string ArchivalType,
        EntityKind Kind,
        InteractionMask Interactions,
        bool HasGravity = false,
        double FallSpeedMultiplier = 1,
        int Variant = 0,
        GravityImpactStrength ImpactStrength = GravityImpactStrength.Ordinary,
        ExplosionReaction? ExplosionReaction = null,
        bool DetonatesOnLanding = false,
        ShakeReaction ShakeReaction = ShakeReaction.None,
        ShakeReaction HardShakeReaction = ShakeReaction.None,
        FireContact? FireContact = null,
        bool TreatsShadowAsSupport = false);
}
