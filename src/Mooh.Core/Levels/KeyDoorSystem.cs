using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.Core.Levels
{
    /// <summary>
    /// Resolves colored doors before ordinary movement collision planning. A matching key only
    /// starts the door's opening; the door stays solid, so the move itself is still blocked.
    /// </summary>
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
                if (archival?.Kind != EntityKind.KeyDoor
                    || occupant.KeyDoorBody is not { OpeningRemainingSeconds: null } door
                    || !progress.HasKey(archival.Variant))
                {
                    continue;
                }

                door.OpeningRemainingSeconds = KeyDoorBody.OpeningSeconds;
                events.Add(new KeyDoorOpeningStarted(player.Id, occupant.Id, archival.Variant));
            }
        }
    }
}

