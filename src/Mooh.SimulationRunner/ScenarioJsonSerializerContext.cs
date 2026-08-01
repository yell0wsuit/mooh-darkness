using System.Text.Json.Serialization;

namespace Mooh.SimulationRunner
{
    /// <summary>Provides AOT-compatible JSON metadata for simulation scenarios.</summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
    [JsonSerializable(typeof(ScenarioDocument))]
    public sealed partial class ScenarioJsonSerializerContext : JsonSerializerContext
    {
    }
}
