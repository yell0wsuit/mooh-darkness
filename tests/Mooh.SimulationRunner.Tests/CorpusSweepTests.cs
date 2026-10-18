using System;
using System.IO;
using System.Linq;

using Mooh.Core.Content;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>
    /// Verifies every committed level survives a seeded random-input bot: the whole corpus is
    /// materialized, entered and played without any simulation path throwing.
    /// </summary>
    public sealed class CorpusSweepTests
    {
        /// <summary>Verifies no committed level throws under the sweep bot.</summary>
        [Fact]
        public void EveryCommittedLevelSurvivesTheSweepBot()
        {
            LoadedLevelPack pack = JsonLevelPackLoader.Load(Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "levels",
                "manifest.json"));

            CorpusSweepResult[] results = [.. CorpusSweep.Run(pack, TimeSpan.FromSeconds(5), seed: 1)];

            Assert.Equal(pack.Levels.Count, results.Length);
            Assert.All(results, result => Assert.Null(result.Failure));
        }

        /// <summary>Verifies the sweep is deterministic for one seed.</summary>
        [Fact]
        public void TheSweepIsDeterministic()
        {
            LoadedLevelPack pack = JsonLevelPackLoader.Load(Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "levels",
                "manifest.json"));
            LoadedLevelPack firstFive = new(pack.Manifest, [.. pack.Levels.Take(5)]);

            CorpusSweepResult[] first = [.. CorpusSweep.Run(firstFive, TimeSpan.FromSeconds(4), seed: 7)];
            CorpusSweepResult[] second = [.. CorpusSweep.Run(firstFive, TimeSpan.FromSeconds(4), seed: 7)];

            Assert.Equal(
                first.Select(result => (result.EventCount, result.Score, result.Outcome)),
                second.Select(result => (result.EventCount, result.Score, result.Outcome)));
        }
    }
}
