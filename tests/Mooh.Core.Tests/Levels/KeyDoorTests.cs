using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Levels
{
    /// <summary>Verifies recovered colored key-door behavior.</summary>
    public sealed class KeyDoorTests
    {
        /// <summary>Verifies a matching collected key removes its door and awards 100 points.</summary>
        [Fact]
        public void MatchingKeyOpensDoorOnApproach()
        {
            MaterializedLevel level = Materialize(includeKey: true);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Contains(events, item => item is KeyDoorOpened opened && opened.DoorEntityId == level.EntityIdsByObjectId[3]);
            Assert.Equal(200, level.World.Player!.PlayerBody!.Progress.Score);
            Assert.DoesNotContain(level.World.Entities, entity => entity.ArchivalIdentity?.Kind == EntityKind.KeyDoor);
        }

        /// <summary>Verifies a door remains solid when its matching key is absent.</summary>
        [Fact]
        public void MissingKeyLeavesDoorBlocked()
        {
            MaterializedLevel level = Materialize(includeKey: false);
            level.Simulation.SetPlayerInput(new PlayerInputState(false, false, true, false));

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Contains(events, item => item is PlayerBlocked);
            Assert.Contains(level.World.Entities, entity => entity.ArchivalIdentity?.Kind == EntityKind.KeyDoor);
        }

        private static MaterializedLevel Materialize(bool includeKey)
        {
            List<LevelObject> objects = [Object(1, "OPlayer", 1, 1)];
            if (includeKey)
            {
                objects.Add(Object(2, "OKDKey1", 2, 1));
            }

            objects.Add(Object(3, "OKDDoor1", includeKey ? 3 : 2, 1));
            objects.Add(Object(4, "CellsSolid01", 2, 2));
            objects.Add(Object(5, "CellsSolid01", 3, 2));
            return LevelMaterializer.Materialize(new LevelDocument(
                1, null, new LevelSize(6, 4), new EditorPreview(0, 0, 6, 4), objects));
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            Dictionary<string, System.Text.Json.JsonElement> properties = [];
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], properties);
        }
    }
}
