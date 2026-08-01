using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.Core.Levels
{
    /// <summary>Resolves colored doors before ordinary movement collision planning.</summary>
    internal static class KeyDoorSystem
    {
        public static void TryOpen(
            Entity player,
            WorldGrid world,
            GridPosition destination,
            ICollection<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(events);
            PlayerProgress progress = player.PlayerBody?.Progress
                ?? throw new InvalidOperationException("Door interaction requires a player.");
            foreach (Entity occupant in world.GetOccupants(destination))
            {
                ArchivalObjectIdentity? archival = occupant.ArchivalIdentity;
                if (archival?.Kind != EntityKind.KeyDoor || !progress.HasKey(archival.Variant))
                {
                    continue;
                }

                _ = world.Remove(occupant.Id);
                int score = progress.OpenDoor();
                events.Add(new KeyDoorOpened(player.Id, occupant.Id, archival.Variant, score));
            }
        }
    }
}
