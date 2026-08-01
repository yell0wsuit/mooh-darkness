using System;
using System.IO;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies deterministic file and directory command-line orchestration.</summary>
    public sealed class ScenarioApplicationTests
    {
        /// <summary>Verifies exactly one existing path is required.</summary>
        [Fact]
        public void ReturnsUsageExitCodeForInvalidArguments()
        {
            using StringWriter output = new();
            using StringWriter error = new();

            int missingArgument = ScenarioApplication.Run([], output, error);
            int missingPath = ScenarioApplication.Run(
                [Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}")],
                output,
                error);
            using StringWriter reservedPathOutput = new();
            using StringWriter reservedPathError = new();
            int reservedPath = ScenarioApplication.Run(
                ["level-board"],
                reservedPathOutput,
                reservedPathError);

            Assert.Equal(2, missingArgument);
            Assert.Equal(2, missingPath);
            Assert.Equal(2, reservedPath);
            Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
            Assert.Contains(
                "Scenario path does not exist: 'level-board'.",
                reservedPathError.ToString(),
                StringComparison.Ordinal);
        }

        /// <summary>Verifies one passing scenario file returns success and prints its name.</summary>
        [Fact]
        public void RunsSinglePassingFile()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string path = Path.Combine(directory, "passing.json");
                File.WriteAllText(path, CreateStationaryScenarioJson("single"));
                using StringWriter output = new();
                using StringWriter error = new();

                int exitCode = ScenarioApplication.Run([path], output, error);

                Assert.True(exitCode == 0, error.ToString());
                Assert.Equal($"PASS single{Environment.NewLine}", output.ToString());
                Assert.Equal(string.Empty, error.ToString());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>Verifies directory runs are ordered, top-level, exhaustive, and aggregated.</summary>
        [Fact]
        public void RunsTopLevelJsonFilesInOrdinalOrderAndContinuesAfterFailure()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, "b-pass.json"), CreateStationaryScenarioJson("second"));
                File.WriteAllText(Path.Combine(directory, "a-fail.json"), CreateUnexpectedFallJson("first"));
                File.WriteAllText(Path.Combine(directory, "c-pass.JSON"), CreateStationaryScenarioJson("third"));
                File.WriteAllText(Path.Combine(directory, "ignored.txt"), CreateStationaryScenarioJson("ignored"));
                string nested = Directory.CreateDirectory(Path.Combine(directory, "nested")).FullName;
                File.WriteAllText(Path.Combine(nested, "ignored.json"), CreateStationaryScenarioJson("nested"));
                using StringWriter output = new();
                using StringWriter error = new();

                int exitCode = ScenarioApplication.Run([directory], output, error);

                Assert.Equal(1, exitCode);
                Assert.Equal(
                    $"FAIL first{Environment.NewLine}PASS second{Environment.NewLine}PASS third{Environment.NewLine}",
                    output.ToString());
                Assert.Contains("a-fail.json", error.ToString(), StringComparison.Ordinal);
                Assert.DoesNotContain("ignored", output.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>Verifies the board command loads an archival level and prints its emoji grid.</summary>
        [Fact]
        public void RendersEmojiBoardForLevelFile()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string path = Path.Combine(directory, "level.json");
                File.WriteAllText(path, CreateLevelJson());
                using StringWriter output = new();
                using StringWriter error = new();

                int exitCode = ScenarioApplication.Run(["level-board", path], output, error);

                Assert.Equal(0, exitCode);
                Assert.Equal(
                    $"Emoji board: level.json (1×1, 1 objects){Environment.NewLine}"
                    + $"🐔{Environment.NewLine}"
                    + $"Legend: 🐔 player{Environment.NewLine}",
                    output.ToString());
                Assert.Equal(string.Empty, error.ToString());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>Verifies invalid archival levels produce a diagnostic and usage exit code.</summary>
        [Fact]
        public void ReportsInvalidLevelForBoardCommand()
        {
            string directory = CreateTemporaryDirectory();
            try
            {
                string path = Path.Combine(directory, "broken.json");
                File.WriteAllText(path, "{}");
                using StringWriter output = new();
                using StringWriter error = new();

                int exitCode = ScenarioApplication.Run(["level-board", path], output, error);

                Assert.Equal(2, exitCode);
                Assert.Equal(string.Empty, output.ToString());
                Assert.Contains("broken.json", error.ToString(), StringComparison.Ordinal);
                Assert.Contains("formatVersion must be 1", error.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), $"mooh-runner-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(path);
            return path;
        }

        private static string CreateStationaryScenarioJson(string name)
        {
            return $$"""
                {
                    "formatVersion": 1,
                    "name": "{{name}}",
                    "world": {
                        "width": 1,
                        "height": 1,
                        "gravity": "Down",
                        "randomSeed": 0,
                        "entities": [
                            {
                                "alias": "object",
                                "position": { "x": 0, "y": 0 },
                                "interactions": []
                            }
                        ]
                    },
                    "steps": [
                        {
                            "advanceMilliseconds": 1,
                            "expectEvents": []
                        }
                    ],
                    "expectFinal": {
                        "entities": [
                            {
                                "alias": "object",
                                "position": { "x": 0, "y": 0 }
                            }
                        ]
                    }
                }
                """;
        }

        private static string CreateUnexpectedFallJson(string name)
        {
            return $$"""
                {
                    "formatVersion": 1,
                    "name": "{{name}}",
                    "world": {
                        "width": 1,
                        "height": 2,
                        "gravity": "Down",
                        "randomSeed": 0,
                        "entities": [
                            {
                                "alias": "rock",
                                "position": { "x": 0, "y": 0 },
                                "interactions": [],
                                "gravityBody": { "fallSpeed": 1, "canSlide": false }
                            }
                        ]
                    },
                    "steps": [
                        {
                            "advanceMilliseconds": 1,
                            "expectEvents": []
                        }
                    ],
                    "expectFinal": {
                        "entities": [
                            {
                                "alias": "rock",
                                "position": { "x": 0, "y": 1 },
                                "motion": {
                                    "cause": "Falling",
                                    "segmentDirection": "Down",
                                    "travelSign": 1,
                                    "speed": 1,
                                    "progress": 0.001,
                                    "resolutionOrder": 0
                                }
                            }
                        ]
                    }
                }
                """;
        }

        private static string CreateLevelJson()
        {
            return /*lang=json,strict*/ """
                {
                    "formatVersion": 1,
                    "size": { "width": 1, "height": 1 },
                    "editorPreview": { "x": 0, "y": 0, "width": 1, "height": 1 },
                    "objects": [
                        {
                            "id": 1,
                            "type": "OPlayer",
                            "editorIndex": 0,
                            "position": { "x": 0, "y": 0 },
                            "savedState": [],
                            "properties": {}
                        }
                    ]
                }
                """;
        }
    }
}
