using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mooh.Presentation.Screens
{
    /// <summary>A recovered map waypoint in the original screen coordinate space.</summary>
    /// <param name="X">Its horizontal coordinate.</param>
    /// <param name="Y">Its vertical coordinate.</param>
    public readonly record struct MapPoint(int X, int Y);

    /// <summary>Reads and holds the map's stage and path anchor points.</summary>
    public sealed class MapWaypoints
    {
        /// <summary>Creates a map from an ordered collection of waypoint coordinates.</summary>
        /// <param name="points">The stage and path coordinates, including the final unused point.</param>
        public MapWaypoints(IReadOnlyList<MapPoint> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            Points = Array.AsReadOnly(points.ToArray());
        }

        /// <summary>Gets the ordered stage and path coordinates.</summary>
        public IReadOnlyList<MapPoint> Points
        {
            get;
        }

        /// <summary>Reads the recovered waypoint file's magic, count and integer coordinate pairs.</summary>
        /// <param name="stream">The waypoint bytes.</param>
        /// <returns>The ordered waypoints.</returns>
        /// <exception cref="InvalidDataException">The header or coordinate data is invalid.</exception>
        public static MapWaypoints Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            ReadOnlySpan<byte> data = copy.GetBuffer().AsSpan(0, (int)copy.Length);
            if (data.Length < 8 || !data[..4].SequenceEqual(new byte[] { 0xae, 0xfa, 0, 0 }))
            {
                throw new InvalidDataException("The map waypoint header is invalid.");
            }

            int count = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
            if (count < 1 || 8L + (count * 8L) > data.Length)
            {
                throw new InvalidDataException("The map waypoint count exceeds its coordinate data.");
            }

            MapPoint[] points = new MapPoint[count];
            for (int index = 0; index < count; index++)
            {
                int offset = 8 + (index * 8);
                points[index] = new MapPoint(BinaryPrimitives.ReadInt32LittleEndian(data[offset..]),
                    BinaryPrimitives.ReadInt32LittleEndian(data[(offset + 4)..]));
            }

            return new MapWaypoints(points);
        }
    }
}
