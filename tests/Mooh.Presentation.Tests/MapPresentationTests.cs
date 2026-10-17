using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Mooh.Core.Content;

using Mooh.Presentation.Screens;
using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks the original map's stage availability, selection and compositing order.</summary>
    public sealed class MapPresentationTests
    {
        /// <summary>The complete campaign's preview files, path frames and localized stories resolve.</summary>
        [Fact]
        public void OriginalCampaignMapReferencesOnlyRecoveredAssets()
        {
            string assets = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../assets"));
            using FileStream coordinates = File.OpenRead(Path.Combine(assets, "map", "map.waypoints"));
            using FileStream text = File.OpenRead(Path.Combine(assets, "text", "strings.bin"));
            StringTable strings = StringTable.Load(text);
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(assets, "levels", "manifest.json")));
            string[] previews = [.. manifest.RootElement.GetProperty("levels").EnumerateArray()
                .Select(level => "map-previews/" + level.GetProperty("name").GetString() + ".jpg")];
            MapPresentation map = new(MapWaypoints.Load(coordinates), previews, key => strings[key]);
            using FileStream frames = File.OpenRead(Path.Combine(assets, "sprites", "SpriteMapPath.json"));
            SpritePage page = SpritePage.Load("SpriteMapPath", frames);
            foreach (int index in Enumerable.Range(0, 30).Select(stage => stage * 5))
            {
                map.Update(index, [], canChange: true, allLevels: true);
                Assert.All(map.Elements.OfType<ScreenImage>(), image => Assert.True(File.Exists(Path.Combine(assets, "screens", image.Name)), image.Name));
                Assert.All(map.Elements.OfType<ScreenSprite>(), sprite => Assert.True(page.Contains(sprite.Frame), sprite.Frame));
                Assert.NotEmpty(map.Elements.OfType<ScreenText>());
            }

            map.Update(149, [.. Enumerable.Range(0, 150)], canChange: false);
            Assert.Contains(map.Nodes, node => node.PathFrame == "map_p_01");
            Assert.All(map.Elements.OfType<ScreenSprite>(), sprite => Assert.True(page.Contains(sprite.Frame), sprite.Frame));
        }

        /// <summary>Malformed waypoint headers and truncated coordinates cannot produce a partial map.</summary>
        [Fact]
        public void RejectsInvalidWaypointData()
        {
            using MemoryStream empty = new();
            _ = Assert.Throws<InvalidDataException>(() => MapWaypoints.Load(empty));
            using MemoryStream truncated = new([0xae, 0xfa, 0, 0, 1, 0, 0, 0]);
            _ = Assert.Throws<InvalidDataException>(() => MapWaypoints.Load(truncated));
        }

        /// <summary>The recovered file contains 150 levels and a final unused waypoint.</summary>
        [Fact]
        public void LoadsTheOriginalWaypoints()
        {
            using FileStream stream = File.OpenRead(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "../../../../../assets/map/map.waypoints")));
            MapWaypoints points = MapWaypoints.Load(stream);
            Assert.Equal(151, points.Points.Count);
            Assert.Equal(new MapPoint(407, 434), points.Points[0]);
            Assert.Equal(new MapPoint(561, 471), points.Points[5]);
        }

        /// <summary>Only passed levels and the first unpassed level are selectable.</summary>
        [Fact]
        public void AvailabilityIncludesPassedLevelsAndOnlyFirstUnpassed()
        {
            MapPresentation map = Create();
            map.Update(0, [0, 1, 2, 3, 4, 10], canChange: true);
            Assert.True(map.Nodes[5].Available);
            Assert.False(map.Nodes[6].Available);
            Assert.True(map.Nodes[10].Available);
            Assert.False(map.Nodes[15].Available);
        }

        /// <summary>Stage previews precede the background overlay, then completed path pieces and markers.</summary>
        [Fact]
        public void DrawsPreviewsUnderBackgroundAndPaths()
        {
            MapPresentation map = Create();
            map.Update(5, [0, 1, 2, 3, 4], canChange: true);
            ScreenElement[] elements = [.. map.Elements];
            ScreenImage[] previews = [.. elements.TakeWhile(element => element is ScreenImage image && image.Name != "MapBack.png").Cast<ScreenImage>()];
            Assert.Equal(30, previews.Length);
            Assert.Equal("preview5.jpg", previews[1].Name);
            Assert.Equal("MapQuestion.png", previews[2].Name);
            _ = Assert.IsType<ScreenImage>(elements[30]);
            Assert.Equal("MapBack.png", ((ScreenImage)elements[30]).Name);
            Assert.Equal("map_p_120", ((ScreenSprite)elements[31]).Frame);
            Assert.Equal("checkpoint_stage", elements.OfType<ScreenSprite>().Last().Frame);
        }

        /// <summary>A locked mid-stage map shows earlier routes, the player marker and the current story.</summary>
        [Fact]
        public void LockedMapFollowsSelectedLevel()
        {
            MapPresentation map = Create();
            map.Update(7, [], canChange: false);
            ScreenSprite marker = map.Elements.OfType<ScreenSprite>().Last();
            Assert.Equal("checkpoint_player", marker.Frame);
            Assert.Equal((70.0, 0.0), (marker.X, marker.Y));
            ScreenText[] story = [.. map.Elements.OfType<ScreenText>()];
            Assert.Equal(["MapStory02 first", "second"], story.Select(text => text.Text));
            Assert.Equal([64.0, 78.0], story.Select(text => text.Y));
            Assert.False(map.SelectNearest(0, 0));
            Assert.Equal(7, map.SelectedIndex);
        }

        /// <summary>Selection chooses the closest preview globally before checking availability.</summary>
        [Fact]
        public void SelectionDoesNotSkipAnUnavailableCloserPreview()
        {
            MapPresentation map = Create();
            map.Update(0, [], canChange: true);
            Assert.False(map.SelectNearest(70.5, 14));
            Assert.Equal(0, map.SelectedIndex);
            map.Update(0, [0, 1, 2, 3, 4], canChange: true);
            Assert.True(map.SelectNearest(70.5, 14));
            Assert.Equal(5, map.SelectedIndex);
        }

        private static MapPresentation Create()
        {
            return new MapPresentation(new MapWaypoints([.. Enumerable.Range(0, 151).Select(index => new MapPoint(index * 10, 0))]),
                [.. Enumerable.Range(0, 150).Select(index => $"preview{index}.jpg")], key => key + " first\n\nsecond");
        }
    }
}
