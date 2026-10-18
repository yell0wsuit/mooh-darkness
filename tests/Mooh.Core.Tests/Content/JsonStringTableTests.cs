using System.IO;
using System.Text;

using Mooh.Core.Content;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    /// <summary>Checks Unicode text and line breaks in JSON string tables.</summary>
    public sealed class JsonStringTableTests
    {
        /// <summary>Checks that loading JSON preserves accented text, punctuation and embedded newlines.</summary>
        [Fact]
        public void JsonRetainsUnicodeAndNewlines()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(/*lang=json,strict*/ "{\"language\":\"Test\",\"strings\":{\"greeting\":\"Café — Tiếng Việt\\nHello\"}}"));
            StringTable table = StringTable.Load(stream);
            Assert.Equal("Test", table.Language);
            Assert.Equal("Café — Tiếng Việt\nHello", table["greeting"]);
        }
    }
}
