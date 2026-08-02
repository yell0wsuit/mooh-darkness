using System;
using System.IO;
using System.Linq;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies all committed end-to-end simulation specifications.</summary>
    public sealed class CommittedScenarioTests
    {
        /// <summary>Verifies all eleven committed scenarios execute successfully.</summary>
        [Fact]
        public void AllCommittedScenariosPass()
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "scenarios");

            Assert.True(Directory.Exists(directory), $"Scenario directory not found: {directory}");
            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            Assert.Equal(11, files.Length);
            ScenarioRunResult[] results =
            [
                .. files.Order(StringComparer.Ordinal).Select(
                    path => ScenarioExecutor.Run(
                        ScenarioLoader.Load(File.ReadAllText(path), path),
                        path)),
            ];

            Assert.All(results, result => Assert.True(result.Passed, result.Diagnostic));
        }
    }
}
