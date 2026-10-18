using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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

        /// <summary>Reads an ordered JSON array of integer coordinate pairs.</summary>
        /// <param name="stream">The waypoint bytes.</param>
        /// <returns>The ordered waypoints.</returns>
        /// <exception cref="InvalidDataException">The coordinate data is invalid.</exception>
        public static MapWaypoints Load(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            try
            {
                using JsonDocument document = JsonDocument.Parse(stream);
                List<MapPoint> points = [];
                foreach (JsonElement point in document.RootElement.EnumerateArray())
                {
                    points.Add(new MapPoint(point.GetProperty("x").GetInt32(), point.GetProperty("y").GetInt32()));
                }
                return points.Count > 0 ? new MapWaypoints(points)
                    : throw new InvalidDataException("The map requires at least one waypoint.");
            }
            catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                throw new InvalidDataException("The map must be an array of integer x/y coordinates.", error);
            }
        }
    }
}
