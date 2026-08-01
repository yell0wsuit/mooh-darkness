using System;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies deterministic scenario JSON loading.</summary>
    public sealed class ScenarioLoaderTests
    {
        /// <summary>Verifies the complete version-one document shape is deserialized.</summary>
        [Fact]
        public void LoadsVersionOneScenario()
        {
            const string json = /*lang=json,strict*/ """
                {
                    "formatVersion": 1,
                    "name": "falling",
                    "world": {
                        "width": 3,
                        "height": 4,
                        "gravity": "Down",
                        "randomSeed": 7,
                        "entities": [
                            {
                                "alias": "rock",
                                "position": { "x": 1, "y": 0 },
                                "interactions": [],
                                "gravityBody": {
                                    "fallSpeed": 1.875,
                                    "canSlide": false
                                }
                            }
                        ]
                    },
                    "steps": [
                        {
                            "advanceMilliseconds": 1000,
                            "expectEvents": []
                        }
                    ],
                    "expectFinal": {
                        "entities": [
                            {
                                "alias": "rock",
                                "position": { "x": 1, "y": 2 }
                            }
                        ]
                    }
                }
                """;

            ScenarioDocument scenario = ScenarioLoader.Load(json, "falling.json");

            Assert.Equal(1, scenario.FormatVersion);
            Assert.Equal("falling", scenario.Name);
            _ = Assert.Single(scenario.World.Entities);
            _ = Assert.Single(scenario.Steps);
        }

        /// <summary>Verifies malformed JSON is reported as source-aware scenario validation.</summary>
        [Fact]
        public void RejectsMalformedJsonWithSource()
        {
            ScenarioValidationException exception = Assert.Throws<ScenarioValidationException>(
                () => ScenarioLoader.Load("{", "broken.json"));

            Assert.StartsWith("broken.json:", exception.Message, StringComparison.Ordinal);
            Assert.Contains("Invalid JSON", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies unknown properties cannot silently weaken a scenario.</summary>
        [Fact]
        public void RejectsUnknownProperty()
        {
            const string json = /*lang=json,strict*/ """
                {
                    "formatVersion": 1,
                    "name": "unknown-property",
                    "world": {
                        "width": 1,
                        "height": 1,
                        "gravity": "Down",
                        "randomSeed": 0,
                        "entities": []
                    },
                    "steps": [],
                    "expectFinal": { "entities": [] },
                    "unexpected": true
                }
                """;

            ScenarioValidationException exception = Assert.Throws<ScenarioValidationException>(
                () => ScenarioLoader.Load(json, "unknown.json"));

            Assert.Contains("unexpected", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Verifies the scenario root must be a JSON object.</summary>
        [Fact]
        public void RejectsNullRoot()
        {
            ScenarioValidationException exception = Assert.Throws<ScenarioValidationException>(
                () => ScenarioLoader.Load("null", "null.json"));

            Assert.Equal("null.json: The JSON root must be an object.", exception.Message);
        }
    }
}
