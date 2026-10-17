using System.IO;
using System.Text;

using Mooh.Core.Content;

using Xunit;

namespace Mooh.Core.Tests.Content
{
    public sealed class JsonStringTableTests
    {
        [Fact]
        public void JsonRetainsUnicodeAndNewlines()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes("{\"language\":\"Test\",\"strings\":{\"greeting\":\"Café — Tiếng Việt\\nHello\"}}"));
            StringTable table = StringTable.Load(stream);
            Assert.Equal("Test", table.Language);
            Assert.Equal("Café — Tiếng Việt\nHello", table["greeting"]);
        }
    }
}
