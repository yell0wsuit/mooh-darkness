using Mooh.Core.Interactions;

namespace Mooh.Core.Legacy
{
    /// <summary>Defines the explicit runtime meaning of one archival object type.</summary>
    /// <param name="LegacyType">The exact case-sensitive archival type name.</param>
    /// <param name="Kind">The semantic runtime family.</param>
    /// <param name="Interactions">The movement interaction capabilities.</param>
    /// <param name="HasGravity">Whether the object participates in directional gravity.</param>
    /// <param name="FallSpeedMultiplier">The multiplier applied to the normal fall speed.</param>
    /// <param name="Variant">The family-specific variant or cardinal direction.</param>
    public sealed record LegacyObjectDescriptor(
        string LegacyType,
        LegacyEntityKind Kind,
        InteractionMask Interactions,
        bool HasGravity = false,
        double FallSpeedMultiplier = 1,
        int Variant = 0);
}
