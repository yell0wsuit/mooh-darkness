using System.IO;
using System.Text;

using Mooh.Presentation.Screens;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks the ordered JSON coordinate contract for the map.</summary>
    public sealed class JsonWaypointsTests
    {
        /// <summary>Checks that positive and negative waypoint coordinates retain their order.</summary>
        [Fact]
        public void CoordinatesKeepTheirOrderAndSign()
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(/*lang=json,strict*/ "[{\"x\":407,\"y\":434},{\"x\":-1,\"y\":5}]"));
            Assert.Equal([new MapPoint(407, 434), new MapPoint(-1, 5)], MapWaypoints.Load(stream).Points);
        }

        /// <summary>Checks that empty maps, missing coordinates and fractional coordinates are rejected.</summary>
        [Theory]
        [InlineData("[]")]
        [InlineData("[{}]")]
        [InlineData(/*lang=json,strict*/ "[{\"x\":0.5,\"y\":1}]")]
        public void InvalidCoordinatesAreRejected(string json)
        {
            using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
            _ = Assert.Throws<InvalidDataException>(() => MapWaypoints.Load(stream));
        }
    }
}
