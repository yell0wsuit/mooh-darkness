using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.Core.Legacy
{
    /// <summary>Applies recovered collection effects for explicitly materialized archival pickups.</summary>
    internal static class LegacyPickupSystem
    {
        public static void Collect(
            Entity player,
            Entity pickup,
            ICollection<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(pickup);
            ArgumentNullException.ThrowIfNull(events);
            PlayerBody playerBody = player.PlayerBody
                ?? throw new InvalidOperationException("Legacy pickup collection requires a player.");
            LegacyEntityBody? legacy = pickup.LegacyBody;
            if (legacy is null)
            {
                return;
            }

            int score = playerBody.Progress.Collect(
                legacy.Descriptor,
                player.HealthBody
                    ?? throw new InvalidOperationException("Legacy pickup collection requires player health."));
            events.Add(new LegacyPickupCollected(player.Id, pickup.Id, legacy.Kind, score));
        }
    }
}
