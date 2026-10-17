using System;
using System.IO;

using Mooh.Core.Content;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    /// <summary>Verifies the recovered localized string table, <c>strings.json</c>.</summary>
    public sealed class StringTableTests
    {
        /// <summary>Verifies the committed table yields its language and every recovered text.</summary>
        [Fact]
        public void TheCommittedTableYieldsTheRecoveredTexts()
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "text", "strings.json"));

            StringTable table = StringTable.Load(stream);

            Assert.Equal("Eng", table.Language);
            Assert.Equal(457, table.Count);
            Assert.Equal("Fire will burn objects.", table["HlpTutAllObjsFire"]);
            Assert.Equal("Dig into the earth \nto move forward.", table["HlpTut01Dig"]);
            Assert.Equal("Level %d", table["GameScrLvl"]);
        }

        /// <summary>
        /// Verifies a text character maps back to its Windows-1252 byte, the code the bitmap fonts
        /// index by, and a character outside the code page has none.
        /// </summary>
        [Fact]
        public void ACharacterMapsBackToItsCodePageByte()
        {
            Assert.True(StringTable.TryEncode('A', out byte letter));
            Assert.Equal(0x41, letter);
            Assert.True(StringTable.TryEncode('\u2019', out byte apostrophe));
            Assert.Equal(0x92, apostrophe);
            Assert.True(StringTable.TryEncode('\u00E9', out byte accented));
            Assert.Equal(0xE9, accented);
            Assert.False(StringTable.TryEncode('\u4E00', out _));
        }

        /// <summary>Verifies a file without the recovered signature is rejected.</summary>
        [Fact]
        public void AFileWithoutTheSignatureIsRejected()
        {
            using MemoryStream stream = new([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]);

            _ = Assert.Throws<InvalidDataException>(() => StringTable.Load(stream));
        }
    }
}
