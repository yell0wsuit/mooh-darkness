using System.Text.Json.Serialization;

namespace Mooh.SimulationRunner
{
    /// <summary>Provides AOT-compatible JSON metadata for interactive play frames.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(PlayFrame))]
    public sealed partial class PlayJsonSerializerContext : JsonSerializerContext
    {
    }
}
