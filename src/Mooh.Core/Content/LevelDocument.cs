using System.Collections.Generic;

namespace Mooh.Core.Content
{
    /// <summary>Represents one archival level document.</summary>
    public sealed record LevelDocument(
        int FormatVersion,
        LevelSource? Source,
        LevelSize Size,
        EditorPreview EditorPreview,
        IReadOnlyList<LevelObject>? Objects);
}
