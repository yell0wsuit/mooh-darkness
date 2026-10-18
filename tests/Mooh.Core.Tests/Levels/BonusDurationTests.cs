using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Levels
{
    /// <summary>Verifies CBonInfo.BonOn adds a pickup's duration to its existing timer.</summary>
    public sealed class BonusDurationTests
    {
        /// <summary>Verifies repeated ice pickups preserve the unused duration through its extended expiry.</summary>
        [Fact]
        public void ASecondIceAddsToTheRemainingDuration()
        {
            List<LevelObject> objects =
            [
                Object(1, "OPlayer", 1, 1),
                Object(2, "BIce", 2, 1),
                Object(3, "BIce", 3, 1),
            ];
            for (int x = 0; x < 8; x++)
            {
                objects.Add(Object(100 + x, "CellsMetal01", x, 2));
            }

            MaterializedLevel level = LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(8, 4), new EditorPreview(0, 0, 8, 4), objects));
            Entity player = level.GetEntity(1);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));
            IReadOnlyList<SimulationEvent> first = level.Simulation.Advance(TimeSpan.FromMilliseconds(300));
            Assert.Contains(first, item => item is PickupCollected { EntityId: var id } && id == player.Id);
            TimeSpan remaining = player.PlayerBody!.Progress.IceRemaining;
            Assert.InRange(remaining, TimeSpan.FromSeconds(44), TimeSpan.FromSeconds(45));

            TimeSpan elapsed = TimeSpan.FromMilliseconds(300);
            IReadOnlyList<SimulationEvent> second = level.Simulation.Advance(elapsed);
            Assert.Contains(second, item => item is PickupCollected { Kind: EntityKind.Ice });
            TimeSpan expected = remaining + TimeSpan.FromSeconds(45) - elapsed;
            Assert.InRange(player.PlayerBody.Progress.IceRemaining,
                expected - TimeSpan.FromMilliseconds(1), expected + TimeSpan.FromMilliseconds(1));

            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromSeconds(45));
            Assert.True(player.PlayerBody.Progress.IceRemaining > TimeSpan.FromSeconds(43));
            _ = level.Simulation.Advance(player.PlayerBody.Progress.IceRemaining);
            Assert.Equal(TimeSpan.Zero, player.PlayerBody.Progress.IceRemaining);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [],
                new Dictionary<string, System.Text.Json.JsonElement>());
        }
    }
}
