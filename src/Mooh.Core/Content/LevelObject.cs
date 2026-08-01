using System.Collections.Generic;
using System.Text.Json;

namespace Mooh.Core.Content
{
    /// <summary>Represents an object preserved from an archival level.</summary>
    public sealed record LevelObject(
        int Id,
        string? Type,
        int EditorIndex,
        LevelPosition Position,
        IReadOnlyList<LevelSavedState>? SavedState,
        IReadOnlyDictionary<string, JsonElement>? Properties);
}
