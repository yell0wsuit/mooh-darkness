namespace Mooh.Core.Content
{
    /// <summary>Identifies the legacy source of an archival level pack.</summary>
    public sealed record LevelPackSource(string? Archive, string? LegacyPackName);
}
