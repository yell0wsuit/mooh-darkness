using System.Text.Json;

namespace Mooh.Core.Content
{
    /// <summary>Preserves one extensible legacy saved-state value.</summary>
    public sealed record LevelSavedState(string? Class, JsonElement Value);
}
