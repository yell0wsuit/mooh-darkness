using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Mooh.Presentation.Screens
{
    /// <summary>One level's preview or path segment and its progression state.</summary>
    /// <param name="Index">The zero-based level index.</param>
    /// <param name="Point">Its waypoint coordinate.</param>
    /// <param name="PreviewName">The stage preview image, or null for a path segment.</param>
    /// <param name="PathFrame">The path frame, or null for a stage preview or unused point.</param>
    /// <param name="Passed">Whether the level has been completed.</param>
    /// <param name="Available">Whether the level may be selected.</param>
    public sealed record MapNode(int Index, MapPoint Point, string? PreviewName, string? PathFrame, bool Passed, bool Available);

    /// <summary>Builds the original thirty-stage campaign map independently of a window or renderer.</summary>
    public sealed partial class MapPresentation
    {
        private readonly MapWaypoints _waypoints;
        private readonly string[] _previewNames;
        private readonly Func<string, string> _localize;
        private bool _canChange;

        /// <summary>Creates a map using the pack's original level names and localized story strings.</summary>
        /// <param name="waypoints">The recovered stage and path coordinates.</param>
        /// <param name="levelPreviewNames">The preview file name for each level, in pack order.</param>
        /// <param name="localize">Looks up a recovered story string by its identifier.</param>
        public MapPresentation(MapWaypoints waypoints, IReadOnlyList<string> levelPreviewNames, Func<string, string> localize)
        {
            ArgumentNullException.ThrowIfNull(waypoints);
            ArgumentNullException.ThrowIfNull(levelPreviewNames);
            ArgumentNullException.ThrowIfNull(localize);
            if (levelPreviewNames.Count is < 1 or > 150 || levelPreviewNames.Count > waypoints.Points.Count)
            {
                throw new ArgumentException("The map requires one to 150 levels with a waypoint for each.", nameof(levelPreviewNames));
            }

            _waypoints = waypoints;
            _previewNames = [.. levelPreviewNames];
            _localize = localize;
            Update(0, [], canChange: true);
        }

        /// <summary>Gets the selected zero-based level index.</summary>
        public int SelectedIndex
        {
            get; private set;
        }

        /// <summary>Gets all stage, path and unused waypoint states.</summary>
        public IReadOnlyList<MapNode> Nodes { get; private set; } = [];

        /// <summary>Gets draw commands in their original compositing order.</summary>
        public IReadOnlyList<ScreenElement> Elements { get; private set; } = [];

        /// <summary>Updates the selected level and the map's saved progression.</summary>
        /// <param name="selectedIndex">The selected zero-based level index.</param>
        /// <param name="passedLevels">The zero-based indices of completed levels.</param>
        /// <param name="canChange">Whether stage selection is enabled rather than showing in-game progress.</param>
        /// <param name="allLevels">Whether the original all-levels option unlocks every stage.</param>
        public void Update(int selectedIndex, IReadOnlyCollection<int> passedLevels, bool canChange, bool allLevels = false)
        {
            ArgumentNullException.ThrowIfNull(passedLevels);
            ArgumentOutOfRangeException.ThrowIfNegative(selectedIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(selectedIndex, _previewNames.Length);
            SelectedIndex = selectedIndex;
            _canChange = canChange;
            HashSet<int> passed = [.. passedLevels];
            bool firstUnpassed = true;
            List<MapNode> nodes = [];
            for (int index = 0; index < _waypoints.Points.Count; index++)
            {
                bool isLevel = index < _previewNames.Length;
                bool completed = isLevel && passed.Contains(index);
                bool available = completed;
                if (isLevel && !completed && firstUnpassed)
                {
                    available = true;
                    firstUnpassed = false;
                }

                available |= isLevel && allLevels;
                bool preview = isLevel && index % 5 == 0 && index != _waypoints.Points.Count - 1;
                nodes.Add(new MapNode(index, _waypoints.Points[index], preview ? _previewNames[index] : null,
                    isLevel && !preview ? $"map_p_{120 - (index * 4 / 5):D2}" : null, completed, available));
            }

            Nodes = nodes.AsReadOnly();
            Rebuild();
        }

        /// <summary>Selects the nearest stage preview, provided that exact stage is available.</summary>
        /// <param name="x">The click's original screen x.</param>
        /// <param name="y">The click's original screen y.</param>
        /// <returns>Whether an available stage accepted the selection.</returns>
        public bool SelectNearest(double x, double y)
        {
            if (!_canChange)
            {
                return false;
            }

            MapNode? nearest = null;
            double distance = double.PositiveInfinity;
            foreach (MapNode node in Nodes.Where(node => node.PreviewName is not null))
            {
                double dx = x - node.Point.X - 20.5;
                double dy = y - node.Point.Y - 14;
                double candidate = (dx * dx) + (dy * dy);
                if (candidate < distance)
                {
                    distance = candidate;
                    nearest = node;
                }
            }

            if (nearest?.Available != true)
            {
                return false;
            }

            SelectedIndex = nearest.Index;
            Rebuild();
            return true;
        }

        private void Rebuild()
        {
            List<ScreenElement> elements = [];
            foreach (MapNode node in Nodes.Where(node => node.PreviewName is not null))
            {
                bool showPreview = _canChange ? node.Available : node.Index <= SelectedIndex;
                elements.Add(new ScreenImage(showPreview ? node.PreviewName! : "MapQuestion.png", node.Point.X, node.Point.Y, 41, 28));
            }

            elements.Add(new ScreenImage("MapBack.png", 0, 0, 800, 600));
            foreach (MapNode node in Nodes.Where(node => node.PathFrame is not null))
            {
                if (_canChange ? node.Passed : node.Index < SelectedIndex)
                {
                    elements.Add(new ScreenSprite("SpriteMapPath", node.PathFrame!));
                }
            }

            MapPoint selected = _waypoints.Points[SelectedIndex];
            if (SelectedIndex % 5 == 0)
            {
                elements.Add(new ScreenSprite("SpriteMapPath", "checkpoint_stage", selected.X, selected.Y));
            }
            else if (!_canChange)
            {
                elements.Add(new ScreenSprite("SpriteMapPath", "checkpoint_player", selected.X, selected.Y));
            }

            string story = _localize($"MapStory{(SelectedIndex / 5) + 1:D2}");
            int y = 64;
            foreach (string line in StoryLineBreaks().Split(story))
            {
                elements.Add(new ScreenText(line, 410, y, "MapStoryFont"));
                y += 14;
            }

            Elements = elements.AsReadOnly();
        }

        [GeneratedRegex("\n+")]
        private static partial Regex StoryLineBreaks();
    }
}
