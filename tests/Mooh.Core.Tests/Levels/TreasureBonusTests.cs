using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Levels
{
    /// <summary>
    /// Verifies the recovered all-treasure bonus. <c>GameInit</c> counts the treasure the player
    /// can reach with <c>_TraceCount</c>, any treasure killed other than by eating forfeits the
    /// bonus, and eating pays it once <c>_Trace</c> finds no reachable treasure left. Only metal
    /// carries <c>CNT_CMaskTracePlLock</c>, so the trace passes through everything else, follows
    /// tunnel maps and jumps teleporter links.
    /// </summary>
    public sealed class TreasureBonusTests
    {
        /// <summary>Verifies treasure sealed in metal is neither counted nor needed for the bonus.</summary>
        [Fact]
        public void TreasureSealedInMetalIsNotNeeded()
        {
            MaterializedLevel level = Materialize(
            [
                Object(1, "OPlayer", 1, 1),
                Object(2, "OCoin01", 2, 1),
                .. SealedCoinAt(7, 1, firstId: 20),
            ]);
            PlayerProgress progress = level.GetEntity(1).PlayerBody!.Progress;
            Assert.Equal(1, progress.TotalTreasure);

            IReadOnlyList<SimulationEvent> events = WalkRight(level);

            Assert.True(progress.HasEarnedTreasureBonus);
            Assert.Contains(events, item => item is PickupCollected { ScoreAwarded: 5001 });
        }

        /// <summary>
        /// Verifies a treasure destroyed by anything but eating forfeits the bonus, so eating the
        /// last remaining one pays nothing.
        /// </summary>
        [Fact]
        public void DestroyingATreasureForfeitsTheBonus()
        {
            MaterializedLevel level = Materialize(
            [
                Object(1, "OPlayer", 1, 1),
                Object(2, "OCoin01", 2, 1),
                Object(3, "OCoin01", 6, 1),
            ]);
            PlayerProgress progress = level.GetEntity(1).PlayerBody!.Progress;
            Assert.Equal(2, progress.TotalTreasure);
            Entity bomb = new(
                EntityId.New(),
                new GridPosition(7, 1),
                Interactions.InteractionMask.None,
                explosionBody: new Explosions.ExplosionBody(Explosions.ExplosionReaction.Detonate3x3, false));
            level.World.Add(bomb);
            _ = level.Simulation.Detonate(bomb.Id);

            _ = level.Simulation.Advance(TimeSpan.FromSeconds(1));
            Assert.False(progress.IsTreasureBonusAvailable);
            IReadOnlyList<SimulationEvent> events = WalkRight(level);

            Assert.False(progress.HasEarnedTreasureBonus);
            Assert.Contains(events, item => item is PickupCollected { ScoreAwarded: 1 });
        }

        /// <summary>
        /// Verifies a teleporter link makes sealed treasure reachable, so it is still needed. The
        /// link lands on the metal wall, which the trace enters anyway: link destinations are
        /// pushed without the <c>CNT_CMaskTracePlLock</c> test.
        /// </summary>
        [Fact]
        public void TeleporterLinksExtendTheTrace()
        {
            MaterializedLevel level = Materialize(
            [
                Object(1, "OPlayer", 1, 1),
                Object(2, "OCoin01", 2, 1),
                Teleporter(3, 0, 1, linkX: 8, linkY: 1),
                .. SealedCoinAt(7, 1, firstId: 20),
            ]);
            PlayerProgress progress = level.GetEntity(1).PlayerBody!.Progress;
            Assert.Equal(2, progress.TotalTreasure);

            IReadOnlyList<SimulationEvent> events = WalkRight(level);

            Assert.False(progress.HasEarnedTreasureBonus);
            Assert.True(progress.IsTreasureBonusAvailable);
            Assert.Contains(events, item => item is PickupCollected { ScoreAwarded: 1 });
        }

        /// <summary>Verifies a level without reachable treasure never offers the bonus.</summary>
        [Fact]
        public void ALevelWithoutReachableTreasureOffersNoBonus()
        {
            MaterializedLevel level = Materialize(
            [
                Object(1, "OPlayer", 1, 1),
                .. SealedCoinAt(7, 1, firstId: 20),
            ]);

            Assert.False(level.GetEntity(1).PlayerBody!.Progress.IsTreasureBonusAvailable);
        }

        private static IReadOnlyList<SimulationEvent> WalkRight(MaterializedLevel level)
        {
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));
            level.Simulation.SetPlayerInput(default);
            return events;
        }

        /// <summary>Places a coin at the given cell enclosed on every side by metal.</summary>
        private static IEnumerable<LevelObject> SealedCoinAt(int x, int y, int firstId)
        {
            yield return Object(firstId, "OCoin01", x, y);
            int id = firstId + 1;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx != 0 || dy != 0)
                    {
                        yield return Object(id++, "CellsMetal01", x + dx, y + dy);
                    }
                }
            }
        }

        private static LevelObject Teleporter(int id, int x, int y, int linkX, int linkY)
        {
            JsonElement links = JsonSerializer.SerializeToElement(new[] { new[] { new[] { linkX, linkY } } });
            return new LevelObject(
                id,
                "OTPort",
                0,
                new LevelPosition(x, y),
                [new LevelSavedState("GameLinkBase.CLinkCoordBase", links)],
                new Dictionary<string, JsonElement>());
        }

        private static MaterializedLevel Materialize(List<LevelObject> objects)
        {
            List<LevelObject> all = [.. objects];
            int floorId = 100;
            for (int x = 0; x <= 9; x++)
            {
                if (!objects.Any(item => item.Position.X == x && item.Position.Y == 2))
                {
                    all.Add(Object(floorId++, "CellsSolid01", x, 2));
                }
            }

            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(10, 4), new EditorPreview(0, 0, 10, 4), all),
                Direction.Down,
                Undrained.Standard);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], new Dictionary<string, JsonElement>());
        }
    }
}
