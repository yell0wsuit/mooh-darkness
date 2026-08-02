using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.Json;

using Mooh.Core.Content;
using Mooh.Core.Player;
using Mooh.Core.Session;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies the interactive play shell drives the simulation and renders it live.</summary>
    public sealed class PlayShellTests
    {
        /// <summary>Verifies the live board renders one glyph per cell from the runtime world.</summary>
        [Fact]
        public void LiveBoardRendersOneGlyphPerCell()
        {
            PlaySession session = CreateSession();

            PlayFrame frame = session.Render();

            Assert.Equal(4, frame.Rows.Count);
            Assert.All(frame.Rows, row => Assert.Equal(6, CountGlyphs(row)));
            Assert.Contains("🐔", frame.Rows[1]);
            Assert.Contains("🔵", frame.Rows[1]);
            Assert.Contains("🧱", frame.Rows[2]);
        }

        /// <summary>Verifies the frame reports the recovered progress values.</summary>
        [Fact]
        public void FrameReportsProgressAndMonsterCount()
        {
            PlaySession session = CreateSession();

            PlayFrame frame = session.Render();

            Assert.Equal(1, frame.Level);
            Assert.Equal(2, frame.LevelCount);
            Assert.Equal(100, frame.Health);
            Assert.Equal(5, frame.Grenades);
            Assert.Equal(1, frame.MonstersLeft);
            Assert.Equal(nameof(GameSessionState.Playing), frame.State);
            Assert.Equal(nameof(PlayerReactionKind.Normal), frame.Reaction);
        }

        /// <summary>Verifies held input moves the player and collects along the way.</summary>
        [Fact]
        public void HeldInputWalksThePlayer()
        {
            FakeTimeProvider time = new();
            PlaySession session = new(CreatePack(), 0, time);
            PlayerInputState right = new(Left: false, Up: false, Right: true, Down: false);

            for (int frameIndex = 0; frameIndex < 10; frameIndex++)
            {
                time.Advance(TimeSpan.FromMilliseconds(50));
                _ = session.Advance(right);
            }

            Assert.Equal(10, session.Session.TotalScore);
        }

        /// <summary>
        /// Verifies a hidden browser tab cannot fast-forward the simulation: one very long frame
        /// interval is clamped rather than replayed in a single step.
        /// </summary>
        [Fact]
        public void LongFrameIntervalIsClamped()
        {
            FakeTimeProvider time = new();
            PlaySession session = new(CreatePack(), 0, time);

            time.Advance(TimeSpan.FromMinutes(5));
            _ = session.Advance(new PlayerInputState(false, false, true, false));

            // 100 ms at the recovered player speed of 2.5 cells per second is a quarter cell,
            // so the player is still short of the coin one cell to the right.
            Assert.Equal(0, session.Session.TotalScore);
        }

        /// <summary>Verifies an unknown path serves the page and the frame path serves JSON.</summary>
        [Fact]
        public void RoutingServesThePageAndFrames()
        {
            PlayServer server = new(CreateSession());

            (string pageType, string page) = server.Route("/", []);
            (string frameType, string frameJson) = server.Route("/frame", []);

            Assert.StartsWith("text/html", pageType, StringComparison.Ordinal);
            Assert.Contains("<title>Mooh</title>", page, StringComparison.Ordinal);
            Assert.StartsWith("application/json", frameType, StringComparison.Ordinal);
            PlayFrame? frame = JsonSerializer.Deserialize(
                frameJson,
                PlayJsonSerializerContext.Default.PlayFrame);
            Assert.Equal(1, frame!.Level);
        }

        /// <summary>Verifies the restart command replays the level through the session.</summary>
        [Fact]
        public void RestartCommandReplaysTheLevel()
        {
            PlaySession session = CreateSession();
            PlayServer server = new(session);
            NameValueCollection restart = new() { { "command", "restart" } };

            _ = server.Route("/frame", restart);

            Assert.Equal(GameSessionState.Playing, session.Session.State);
            Assert.Equal(0, session.Session.LevelIndex);
        }

        private static int CountGlyphs(string row)
        {
            return new System.Globalization.StringInfo(row).LengthInTextElements;
        }

        private static PlaySession CreateSession()
        {
            return new PlaySession(CreatePack());
        }

        private static LoadedLevelPack CreatePack()
        {
            return new LoadedLevelPack(
                new LevelPackManifest(
                    1,
                    null,
                    [new LevelManifestEntry(0, "one", "one.json"), new LevelManifestEntry(1, "two", "two.json")]),
                [CreateLevel(), CreateLevel()]);
        }

        private static LevelDocument CreateLevel()
        {
            List<LevelObject> objects =
            [
                Object(1, "OPlayer", 1, 1),
                Object(2, "OCoin03", 2, 1),
                Object(3, "MBlueR", 4, 1),
                Object(4, "OCoin01", 0, 1),
                Object(5, "CellsSolid01", 0, 2),
                Object(6, "CellsSolid01", 1, 2),
                Object(7, "CellsSolid01", 2, 2),
                Object(8, "CellsSolid01", 4, 2),
            ];
            return new LevelDocument(
                1,
                null,
                new LevelSize(6, 4),
                new EditorPreview(0, 0, 6, 4),
                objects);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            Dictionary<string, JsonElement> properties = [];
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], properties);
        }

        private sealed class FakeTimeProvider : TimeProvider
        {
            private long _timestamp;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp()
            {
                return _timestamp;
            }

            public void Advance(TimeSpan amount)
            {
                _timestamp += amount.Ticks;
            }
        }
    }
}
