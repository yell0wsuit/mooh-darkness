using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.Core.Combat
{
    /// <summary>Resolves health damage without presentation side effects.</summary>
    public static class DamageSystem
    {
        /// <summary>Applies a validated request to its target and records the outcome.</summary>
        /// <param name="target">The entity identified by the request target.</param>
        /// <param name="request">The requested damage.</param>
        /// <param name="events">The ordered event destination.</param>
        /// <returns>The damage outcome.</returns>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The request identifies a different target.</exception>
        /// <exception cref="InvalidOperationException">The target has no health component.</exception>
        public static DamageResult Apply(
            Entity target,
            DamageRequest request,
            ICollection<SimulationEvent> events)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(events);
            if (target.Id != request.TargetEntityId)
            {
                throw new ArgumentException("The damage request does not identify the supplied target.", nameof(request));
            }

            HealthBody health = target.HealthBody
                ?? throw new InvalidOperationException("Damage requires a target with health.");
            if (health.IsDead)
            {
                events.Add(new DamageIgnored(
                    target.Id,
                    request.SourceEntityId,
                    request.Kind,
                    DamageResult.IgnoredDead));
                return DamageResult.IgnoredDead;
            }

            if (target.PlayerBody?.Progress.ColaRemaining > TimeSpan.Zero
                || health.ImmunityRemaining > TimeSpan.Zero
                || target.PlayerBody?.Reaction.Kind == PlayerReactionKind.Shocked)
            {
                events.Add(new DamageIgnored(
                    target.Id,
                    request.SourceEntityId,
                    request.Kind,
                    DamageResult.IgnoredImmune));
                return DamageResult.IgnoredImmune;
            }

            health.Apply(request.Amount);
            events.Add(new DamageApplied(
                target.Id,
                request.SourceEntityId,
                request.Kind,
                request.Amount,
                health.Current));
            return DamageResult.Applied;
        }
    }
}
