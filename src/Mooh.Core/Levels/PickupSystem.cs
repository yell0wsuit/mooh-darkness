using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.Core.Levels
{
    /// <summary>Applies recovered collection effects for explicitly materialized archival pickups.</summary>
    internal static class PickupSystem
    {
        public static void Collect(
            Entity player,
            Entity pickup,
            DifficultySpeeds difficultySpeeds,
            ICollection<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(pickup);
            ArgumentNullException.ThrowIfNull(difficultySpeeds);
            ArgumentNullException.ThrowIfNull(events);
            PlayerBody playerBody = player.PlayerBody
                ?? throw new InvalidOperationException("Pickup collection requires a player.");
            if (pickup.GreenMonsterBody is { IsSmall: true })
            {
                playerBody.Progress.AddScore(10);
                return;
            }

            ArchivalObjectIdentity? archival = pickup.ArchivalIdentity;
            if (archival is null)
            {
                return;
            }

            int score = playerBody.Progress.Collect(
                archival.Descriptor,
                player.HealthBody
                    ?? throw new InvalidOperationException("Pickup collection requires player health."),
                difficultySpeeds.FoodHeal);
            events.Add(new PickupCollected(player.Id, pickup.Id, archival.Kind, score));
            if (archival.Kind == EntityKind.Cola)
            {
                playerBody.HasPendingColaDrink = true;
            }
        }
    }
}
