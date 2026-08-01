using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.Core.Legacy
{
    /// <summary>Resolves colored doors before ordinary movement collision planning.</summary>
    internal static class LegacyDoorSystem
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
                LegacyEntityBody? legacy = occupant.LegacyBody;
                if (legacy?.Kind != LegacyEntityKind.KeyDoor || !progress.HasKey(legacy.Variant))
                {
                    continue;
                }

                _ = world.Remove(occupant.Id);
                int score = progress.OpenDoor();
                events.Add(new LegacyDoorOpened(player.Id, occupant.Id, legacy.Variant, score));
            }
        }
    }
}
