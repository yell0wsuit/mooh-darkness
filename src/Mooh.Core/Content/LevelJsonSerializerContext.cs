using System.Text.Json.Serialization;

namespace Mooh.Core.Content
{
    /// <summary>Provides source-generated JSON metadata for level content.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(LevelDocument))]
    internal sealed partial class LevelJsonSerializerContext : JsonSerializerContext
    {
    }
}
