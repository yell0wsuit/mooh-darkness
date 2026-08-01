using System.Collections.Generic;

namespace Mooh.Core.Content
{
    /// <summary>Describes an archival level pack and its ordered entries.</summary>
    public sealed record LevelPackManifest(
        int FormatVersion,
        LevelPackSource? Source,
        IReadOnlyList<LevelManifestEntry>? Levels);
}
