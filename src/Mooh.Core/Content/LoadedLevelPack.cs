using System.Collections.Generic;

namespace Mooh.Core.Content
{
    /// <summary>Contains a validated pack manifest and its loaded archival levels.</summary>
    public sealed record LoadedLevelPack(
        LevelPackManifest Manifest,
        IReadOnlyList<LevelDocument> Levels);
}
