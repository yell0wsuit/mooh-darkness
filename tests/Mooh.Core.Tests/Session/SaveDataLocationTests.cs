using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Core.Session;

using Xunit;

namespace Mooh.Core.Tests.Session
{
    /// <summary>Verifies portable and platform fallback save directories.</summary>
    public sealed class SaveDataLocationTests
    {
        /// <summary>Portable runs use the requested folder and the regular profile filename.</summary>
        [Fact]
        public void PortableSaveFolderIsPreferredAndUsesTheRequestedJsonName()
        {
            string root = Path.Combine(Path.GetTempPath(), "mooh-path-tests");
            string chosen = SaveDataLocation.ResolveDirectory(Path.Combine(root, "exe"), Path.Combine(root, "documents"), Path.Combine(root, "local"), _ => true);
            Assert.Equal(Path.Combine(root, "exe", "MoohDarkness_SaveData"), chosen);
            Assert.Equal("player-profile.json", Path.GetFileName(new PlayerProfileStore(chosen).FilePath));
        }

        /// <summary>Signed application bundles do not receive portable save files.</summary>
        [Fact]
        public void AppBundleSkipsPortableDirectoryAndUsesDocuments()
        {
            List<string> attempts = [];
            string root = Path.Combine(Path.GetTempPath(), "mooh-path-tests");
            string chosen = SaveDataLocation.ResolveDirectory(Path.Combine(root, "Mooh.app", "Contents", "MacOS"), Path.Combine(root, "documents"), Path.Combine(root, "local"), path => { attempts.Add(path); return true; });
            Assert.Equal(Path.Combine(root, "documents", "MoohDarkness_SaveData"), chosen);
            _ = Assert.Single(attempts);
        }

        /// <summary>Local application data follows the portable and Documents candidates.</summary>
        [Fact]
        public void UnwritablePortableAndDocumentsFallBackToLocalData()
        {
            string root = Path.Combine(Path.GetTempPath(), "mooh-path-tests");
            string chosen = SaveDataLocation.ResolveDirectory(Path.Combine(root, "exe"), Path.Combine(root, "documents"), Path.Combine(root, "local"), path => path.StartsWith(Path.Combine(root, "local"), StringComparison.Ordinal));
            Assert.Equal(Path.Combine(root, "local", "MoohDarkness_SaveData"), chosen);
        }
    }
}
