using System.IO;
using System.Text;

using Mooh.Presentation.Screens;

using Xunit;

namespace Mooh.Presentation.Tests
{
    public sealed class JsonWaypointsTests
    {
        [Fact]
        public void CoordinatesKeepTheirOrderAndSign()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes("[{\"x\":407,\"y\":434},{\"x\":-1,\"y\":5}]"));
            Assert.Equal([new MapPoint(407, 434), new MapPoint(-1, 5)], MapWaypoints.Load(stream).Points);
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("[{}]")]
        [InlineData("[{\"x\":0.5,\"y\":1}]")]
        public void InvalidCoordinatesAreRejected(string json)
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
            Assert.Throws<InvalidDataException>(() => MapWaypoints.Load(stream));
        }
    }
}
