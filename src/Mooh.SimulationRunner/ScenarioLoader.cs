using System;
using System.Text.Json;

namespace Mooh.SimulationRunner
{
    /// <summary>Loads deterministic simulation scenarios from JSON.</summary>
    public static class ScenarioLoader
    {
        /// <summary>Loads a scenario document from JSON text.</summary>
        /// <param name="json">The JSON document text.</param>
        /// <param name="source">The source name used in diagnostics.</param>
        /// <returns>The deserialized scenario document.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="json"/> or <paramref name="source"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ScenarioValidationException">The JSON cannot be deserialized.</exception>
        public static ScenarioDocument Load(string json, string source)
        {
            ArgumentNullException.ThrowIfNull(json);
            ArgumentNullException.ThrowIfNull(source);

            try
            {
                return JsonSerializer.Deserialize(
                    json,
                    ScenarioJsonSerializerContext.Default.ScenarioDocument)
                    ?? throw new ScenarioValidationException(source, "The JSON root must be an object.");
            }
            catch (JsonException exception)
            {
                string path = string.IsNullOrEmpty(exception.Path) ? "$" : exception.Path;
                throw new ScenarioValidationException(
                    source,
                    $"Invalid JSON at '{path}': {exception.Message}",
                    exception);
            }
        }
    }
}
