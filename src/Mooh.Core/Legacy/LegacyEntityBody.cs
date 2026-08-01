using System;

using Mooh.Core.Content;

namespace Mooh.Core.Legacy
{
    /// <summary>Associates a runtime entity with its validated archival source and semantic descriptor.</summary>
    public sealed class LegacyEntityBody(LevelObject source, LegacyObjectDescriptor descriptor)
    {
        /// <summary>Gets the original validated object declaration.</summary>
        public LevelObject Source { get; } = source ?? throw new ArgumentNullException(nameof(source));

        /// <summary>Gets the explicit catalog descriptor.</summary>
        public LegacyObjectDescriptor Descriptor { get; } = descriptor
            ?? throw new ArgumentNullException(nameof(descriptor));

        /// <summary>Gets the archival object identifier.</summary>
        public int ObjectId => Source.Id;

        /// <summary>Gets the exact archival type name.</summary>
        public string LegacyType => Descriptor.LegacyType;

        /// <summary>Gets the semantic runtime family.</summary>
        public LegacyEntityKind Kind => Descriptor.Kind;

        /// <summary>Gets the family-specific variant or direction.</summary>
        public int Variant => Descriptor.Variant;
    }
}
