using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Content;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    /// <summary>Verifies the complete committed archival level corpus.</summary>
    public sealed class AuthoritativeLevelCorpusTests
    {
        /// <summary>Verifies every authoritative level satisfies the archival schema.</summary>
        [Fact]
        public void CompleteAuthoritativeCorpusIsValid()
        {
            string manifestPath = Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "levels",
                "manifest.json");

            LoadedLevelPack pack = JsonLevelPackLoader.Load(manifestPath);

            Assert.Equal(150, pack.Levels.Count);
            Assert.Equal(81_211, pack.Levels.Sum(level => level.Objects!.Count));
            HashSet<string> types = pack.Levels
                .SelectMany(level => level.Objects!)
                .Select(levelObject => levelObject.Type!)
                .ToHashSet(StringComparer.Ordinal);
            Assert.Equal(85, types.Count);
            Assert.All(
                pack.Levels,
                level => Assert.Single(level.Objects!, item => item.Type == "OPlayer"));
            Assert.Equal(
                Enumerable.Range(1, 150),
                pack.Manifest.Levels!.Select(entry => entry.Order));
        }
    }
}
