using System;
using System.Collections.Generic;
using System.Text.Json;

using Mooh.Core.Combat;
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
    /// Verifies the recovered level start and healing shelter. <c>GamePlayer.GameInit</c> spawns a
    /// door in the player's cell and plays <c>ST_GStartInStart</c> (1500 ms) and
    /// <c>ST_GStartInInWalk</c> (375 ms), immortal and uncontrollable; the door then closes and is
    /// killed 900 ms later. Firing on it first runs the shelter: 875 ms out, 3000 ms hidden, a full
    /// heal, 1125 ms back, after which the door stays for good.
    /// </summary>
    public sealed class LevelEntryShelterTests
    {
        /// <summary>Verifies the player cannot act or be hurt while emerging.</summary>
        [Fact]
        public void ThePlayerEmergesBeforeItCanAct()
        {
            MaterializedLevel level = Materialize();
            Entity player = level.GetEntity(1);

            _ = level.Simulation.BeginLevel();
            Assert.Contains(level.World.GetOccupants(player.Position), occupant => occupant.DoorBody is not null);
            level.Simulation.SetPlayerInput(Right());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(1800));

            Assert.Equal(new GridPosition(1, 1), player.Position);
            Assert.True(player.PlayerBody!.IsImmortal);
            IReadOnlyList<SimulationEvent> hit = level.Simulation.ApplyDamage(
                new DamageRequest(player.Id, player.Id, DamageKind.Fire, 10));
            Assert.Contains(hit, item => item is DamageIgnored);

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(500));

            Assert.False(player.PlayerBody.IsImmortal);
            Assert.NotEqual(new GridPosition(1, 1), player.Position);
        }

        /// <summary>Verifies an unused door closes and goes 900 ms after the player is out.</summary>
        [Fact]
        public void AnUnusedDoorGoesAfterThePlayerIsOut()
        {
            MaterializedLevel level = Materialize();
            _ = level.Simulation.BeginLevel();

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(1875 + 890));
            Assert.Contains(level.World.Entities, entity => entity.DoorBody is not null);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(20));

            Assert.DoesNotContain(level.World.Entities, entity => entity.DoorBody is not null);
        }

        /// <summary>
        /// Verifies firing on the door heals the player fully after the hidden wait, and the door
        /// then stays to be used again.
        /// </summary>
        [Fact]
        public void FiringOnTheDoorHealsAndKeepsIt()
        {
            MaterializedLevel level = Materialize();
            Entity player = level.GetEntity(1);
            _ = level.Simulation.ApplyDamage(new DamageRequest(player.Id, player.Id, DamageKind.Fire, 60));
            _ = level.Simulation.Advance(TimeSpan.FromSeconds(1));
            _ = level.Simulation.BeginLevel();
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(1880));

            level.Simulation.SetPlayerInput(Fire());
            IReadOnlyList<SimulationEvent> enter = level.Simulation.Advance(TimeSpan.FromMilliseconds(10));
            level.Simulation.SetPlayerInput(default);
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(3860));
            double beforeHeal = player.HealthBody!.Current;
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(20));

            Assert.Contains(enter, item => item is PlayerShelterEntered);
            Assert.Equal(40, beforeHeal);
            Assert.Equal(100, player.HealthBody.Current);
            Assert.True(player.PlayerBody!.IsImmortal);

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(1200));
            Assert.False(player.PlayerBody.IsImmortal);
            _ = level.Simulation.Advance(TimeSpan.FromSeconds(5));
            Assert.Contains(level.World.Entities, entity => entity.DoorBody is not null);
        }

        private static PlayerInputState Right()
        {
            return new PlayerInputState(Left: false, Up: false, Right: true, Down: false);
        }

        private static PlayerInputState Fire()
        {
            return new PlayerInputState(false, false, false, false, Fire: true);
        }

        private static MaterializedLevel Materialize()
        {
            List<LevelObject> objects = [Object(1, "OPlayer", 1, 1)];
            for (int x = 0; x <= 5; x++)
            {
                objects.Add(Object(10 + x, "CellsSolid01", x, 2));
            }

            return LevelMaterializer.Materialize(
                new LevelDocument(1, null, new LevelSize(6, 4), new EditorPreview(0, 0, 6, 4), objects),
                Direction.Down,
                Undrained.Standard);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], new Dictionary<string, JsonElement>());
        }
    }
}
