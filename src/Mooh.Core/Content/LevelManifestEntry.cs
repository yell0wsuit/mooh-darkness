namespace Mooh.Core.Content
{
    /// <summary>Identifies one ordered level file in a pack manifest.</summary>
    public sealed record LevelManifestEntry(int Order, string? Name, string? File);
}
