using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json;

using Mooh.Core.Content;
using Mooh.Core.Grid;
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

            string[] board = LiveBoardRenderer.Render(session.Session.Level.World);

            Assert.Equal(4, board.Length);
            Assert.All(board, row => Assert.Equal(6, CountGlyphs(row)));
            Assert.Contains("🐔", board[1]);
            Assert.Contains("🔵", board[1]);
            Assert.Contains("🧱", board[2]);
        }

        /// <summary>Verifies a stationary entity is drawn exactly on its authoritative cell.</summary>
        [Fact]
        public void StationarySpritesSitOnWholeCells()
        {
            PlaySession session = CreateSession();

            PlayFrame frame = session.Render();

            Assert.Equal(6, frame.Width);
            Assert.Equal(4, frame.Height);
            PlaySprite player = Assert.Single(frame.Sprites, sprite => sprite.Glyph == "🐔");
            Assert.Equal(1, player.X);
            Assert.Equal(1, player.Y);
        }

        /// <summary>
        /// Verifies a moving entity is drawn between cells. The destination cell is committed when
        /// movement starts, so the drawn position trails it by the unfinished fraction.
        /// </summary>
        [Fact]
        public void MovingSpritesAreDrawnBetweenCells()
        {
            FakeTimeProvider time = new();
            PlaySession session = new(CreatePack(), 0, time);

            // The player materializes facing down, so holding down starts walking with no
            // rotation first. 100 ms at the recovered 2.5 cells per second is a quarter cell.
            time.Advance(TimeSpan.FromMilliseconds(100));
            PlayFrame frame = session.Advance(new PlayerInputState(false, false, false, true));

            PlaySprite player = Assert.Single(frame.Sprites, sprite => sprite.Glyph == "🐔");
            Assert.Equal(1, player.X);
            Assert.Equal(1.25, player.Y, 3);
        }

        /// <summary>Verifies sprites are ordered so gameplay objects draw over terrain.</summary>
        [Fact]
        public void SpritesAreOrderedForDrawing()
        {
            PlaySession session = CreateSession();

            PlayFrame frame = session.Render();

            int[] layers = [.. frame.Sprites.Select(sprite => sprite.Layer)];
            Assert.Equal([.. layers.Order()], layers);
            PlaySprite player = Assert.Single(frame.Sprites, sprite => sprite.Glyph == "🐔");
            PlaySprite wall = frame.Sprites.First(sprite => sprite.Glyph == "🧱");
            Assert.True(player.Layer > wall.Layer);
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

        /// <summary>
        /// Verifies a blast draws over every entity for the recovered cell lock and then clears.
        /// An explosion is a timed cell effect rather than an entity, so the shell has to keep it
        /// from the reach events instead of reading it back out of the world.
        /// </summary>
        [Fact]
        public void ABlastDrawsOverEveryEntityUntilItsCellLockExpires()
        {
            FakeTimeProvider time = new();
            PlaySession session = new(CreateExplosionPack(), 0, time);
            PlayerInputState idle = default;

            PlayFrame? blastFrame = null;
            for (int frameIndex = 0; frameIndex < 20 && blastFrame is null; frameIndex++)
            {
                time.Advance(TimeSpan.FromMilliseconds(50));
                PlayFrame frame = session.Advance(idle);
                if (frame.Sprites.Any(sprite => sprite.Glyph == "💥"))
                {
                    blastFrame = frame;
                }
            }

            Assert.NotNull(blastFrame);
            Assert.Equal("💥", blastFrame.Sprites[^1].Glyph);
            Assert.Contains(
                blastFrame.Sprites,
                sprite => sprite.Glyph == "💥" && sprite.X == 4 && sprite.Y == 1);

            // The recovered lock is 500 ms, so the blast is gone well inside a second.
            for (int frameIndex = 0; frameIndex < 20; frameIndex++)
            {
                time.Advance(TimeSpan.FromMilliseconds(50));
                _ = session.Advance(idle);
            }

            Assert.DoesNotContain(session.Render().Sprites, sprite => sprite.Glyph == "💥");
        }

        /// <summary>
        /// Verifies a cell an immortal object survives in is never drawn as a blast. The recovered
        /// <c>_ObjExplWrd</c> locks such a cell but skips <c>SfxExplAdd1x1</c>, because the cell is
        /// not empty once the destruction pass has run.
        /// </summary>
        [Fact]
        public void ABlastSkipsACellAnImmortalObjectSurvivesIn()
        {
            FakeTimeProvider time = new();
            PlaySession session = new(CreateExplosionPack(), 0, time);

            // The blast is centred on (4, 1) and reaches its neighbours 200 ms later, so the frame
            // to inspect is the one where the horizontal neighbours have been reached.
            PlayFrame? blastFrame = null;
            for (int frameIndex = 0; frameIndex < 30 && blastFrame is null; frameIndex++)
            {
                time.Advance(TimeSpan.FromMilliseconds(50));
                PlayFrame frame = session.Advance(default);
                if (frame.Sprites.Any(
                    sprite => sprite.Glyph == "💥" && sprite.X == 3 && sprite.Y == 1))
                {
                    blastFrame = frame;
                }
            }

            // The metal wall at (5, 1) is immortal and survives, while (3, 1) is cleared.
            Assert.NotNull(blastFrame);
            Assert.DoesNotContain(
                blastFrame.Sprites,
                sprite => sprite.Glyph == "💥" && sprite.X == 5 && sprite.Y == 1);
            Assert.Contains(
                session.Session.Level.World.Entities,
                entity => entity.Position == new GridPosition(5, 1));
        }

        /// <summary>Verifies restarting a level drops any blast left on screen.</summary>
        [Fact]
        public void RestartingTheLevelClearsTheBlast()
        {
            FakeTimeProvider time = new();
            PlaySession session = new(CreateExplosionPack(), 0, time);

            bool blasted = false;
            for (int frameIndex = 0; frameIndex < 20 && !blasted; frameIndex++)
            {
                time.Advance(TimeSpan.FromMilliseconds(50));
                blasted = session.Advance(default).Sprites.Any(sprite => sprite.Glyph == "💥");
            }

            Assert.True(blasted, "the level never produced a blast to clear");
            session.RestartLevel();

            Assert.DoesNotContain(session.Render().Sprites, sprite => sprite.Glyph == "💥");
        }

        /// <summary>
        /// Builds a pack whose single level drops a grenade onto a floor, which detonates it on
        /// landing well away from the player. The immortal metal wall beside the blast is what a
        /// reached cell looks like when nothing in it can be destroyed.
        /// </summary>
        private static LoadedLevelPack CreateExplosionPack()
        {
            List<LevelObject> objects =
            [
                Object(1, "OPlayer", 0, 1),
                Object(2, "WBomb", 4, 0),
                Object(9, "CellsMetal01", 5, 1),
                Object(3, "CellsSolid01", 0, 2),
                Object(4, "CellsSolid01", 1, 2),
                Object(5, "CellsSolid01", 2, 2),
                Object(6, "CellsSolid01", 3, 2),
                Object(7, "CellsSolid01", 4, 2),
                Object(8, "CellsSolid01", 5, 2),
            ];
            LevelDocument level = new(
                1,
                null,
                new LevelSize(6, 4),
                new EditorPreview(0, 0, 6, 4),
                objects);
            return new LoadedLevelPack(
                new LevelPackManifest(1, null, [new LevelManifestEntry(0, "boom", "boom.json")]),
                [level]);
        }

        private static int CountGlyphs(string row)
        {
            return new System.Globalization.StringInfo(row).LengthInTextElements;
        }

        private static PlaySession CreateSession()
        {
            return new PlaySession(CreatePack());
        }

        /// <summary>
        /// Verifies a world rotation reaches the shell. The frame reports the world's dimensions
        /// per frame and the page resizes its canvas from them, so the swap needs no bookkeeping
        /// here; this pins that down, because a cached board size would survive every other test.
        /// </summary>
        [Fact]
        public void AWorldRotationSwapsTheReportedBoardSize()
        {
            FakeTimeProvider time = new();
            PlaySession session = new(CreateRotatorPack(), 0, time);
            PlayFrame before = session.Render();

            Assert.Equal(6, before.Width);
            Assert.Equal(4, before.Height);

            // The player materializes facing down and the rotator sits directly below it, so
            // holding fire strikes the rotator. The strike, the 1000 ms windup and the 785 ms spin
            // together need about two seconds, taken in the shell's clamped 100 ms frames.
            PlayFrame frame = before;
            for (int step = 0; step < 25; step++)
            {
                time.Advance(TimeSpan.FromMilliseconds(100));
                frame = session.Advance(new PlayerInputState(false, false, false, false, true));
            }

            Assert.Equal(4, frame.Width);
            Assert.Equal(6, frame.Height);
            PlaySprite player = Assert.Single(frame.Sprites, sprite => sprite.Glyph == "🐔");
            Assert.InRange(player.X, 0, 3);
            Assert.InRange(player.Y, 0, 5);
        }

        /// <summary>
        /// Verifies a read message table shows its recovered help-text id, and that the shell keeps
        /// it up for the recovered <c>CNT_HlpShTime</c> hold before clearing it.
        /// </summary>
        [Fact]
        public void AReadTableShowsItsHelpTextForTheRecoveredHold()
        {
            FakeTimeProvider time = new();
            PlaySession session = new(CreateTablePack(), 0, time);
            PlayerInputState down = new(Left: false, Up: false, Right: false, Down: true);

            PlayFrame frame = session.Render();
            Assert.Null(frame.HelpText);
            for (int step = 0; step < 5; step++)
            {
                time.Advance(TimeSpan.FromMilliseconds(100));
                frame = session.Advance(step < 3 ? down : default);
            }

            Assert.Equal("HlpTutAllObjsFire", frame.HelpText);
            for (int step = 0; step < 48; step++)
            {
                time.Advance(TimeSpan.FromMilliseconds(100));
                frame = session.Advance(default);
            }

            Assert.Equal("HlpTutAllObjsFire", frame.HelpText);
            for (int step = 0; step < 3; step++)
            {
                time.Advance(TimeSpan.FromMilliseconds(100));
                frame = session.Advance(default);
            }

            Assert.Null(frame.HelpText);
        }

        /// <summary>
        /// Verifies timed play reports the recovered clock, which replaces health, and that an
        /// untimed session reports none.
        /// </summary>
        [Fact]
        public void TimedPlayReportsTheClock()
        {
            FakeTimeProvider time = new();
            PlaySession timed = new(CreatePack(), 0, time, Core.Simulation.DifficultySpeeds.Timed);
            PlaySession standard = new(CreatePack(), 0, time);

            time.Advance(TimeSpan.FromMilliseconds(100));
            PlayFrame frame = timed.Advance(default);

            Assert.Equal(399.9, frame.TimeRemaining!.Value, 3);
            Assert.Equal(100, frame.Health);
            Assert.Null(standard.Render().TimeRemaining);
        }

        /// <summary>Verifies the play command rejects a difficulty outside the recovered table.</summary>
        [Fact]
        public void PlayRejectsAnUnknownDifficulty()
        {
            using System.IO.StringWriter output = new();
            using System.IO.StringWriter error = new();

            int exitCode = ScenarioApplication.Run(["play", "--difficulty", "3"], output, error);

            Assert.Equal(2, exitCode);
            Assert.Contains("--difficulty", error.ToString(), StringComparison.Ordinal);
        }

        private static LoadedLevelPack CreateTablePack()
        {
            Dictionary<string, JsonElement> properties = new()
            {
                ["_TableTxt"] = JsonSerializer.SerializeToElement("HlpTutAllObjsFire"),
            };
            LevelDocument level = new(
                1,
                null,
                new LevelSize(3, 4),
                new EditorPreview(0, 0, 3, 4),
                [
                    Object(1, "OPlayer", 1, 1),
                    new LevelObject(2, "OTable", 0, new LevelPosition(1, 2), [], properties),
                    Object(3, "CellsMetal01", 1, 3),
                ]);
            return new LoadedLevelPack(
                new LevelPackManifest(1, null, [new LevelManifestEntry(0, "table", "table.json")]),
                [level]);
        }

        private static LoadedLevelPack CreateRotatorPack()
        {
            LevelDocument level = new(
                1,
                null,
                new LevelSize(6, 4),
                new EditorPreview(0, 0, 6, 4),
                [Object(1, "OPlayer", 1, 1), Object(2, "OWRotL", 1, 2)]);
            return new LoadedLevelPack(
                new LevelPackManifest(1, null, [new LevelManifestEntry(0, "one", "one.json")]),
                [level]);
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
