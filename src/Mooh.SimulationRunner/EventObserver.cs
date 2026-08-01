using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Simulation;

namespace Mooh.SimulationRunner
{
    /// <summary>Normalizes core simulation events into stable scenario values.</summary>
    public static class EventObserver
    {
        /// <summary>Observes an ordered core event sequence.</summary>
        /// <param name="events">The ordered events returned by one simulation advance.</param>
        /// <param name="aliasesById">Scenario aliases keyed by runtime entity identifier.</param>
        /// <returns>The ordered normalized events.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="events"/> or <paramref name="aliasesById"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">An event or entity identifier is unsupported.</exception>
        public static IReadOnlyList<ObservedEvent> Observe(
            IReadOnlyList<SimulationEvent> events,
            IReadOnlyDictionary<EntityId, string> aliasesById)
        {
            ArgumentNullException.ThrowIfNull(events);
            ArgumentNullException.ThrowIfNull(aliasesById);

            List<ObservedEvent> result = [];
            foreach (SimulationEvent simulationEvent in events)
            {
                string entity = ResolveAlias(simulationEvent.EntityId, aliasesById);
                result.Add(simulationEvent switch
                {
                    CellEntered value => new ObservedEvent(
                        nameof(CellEntered),
                        entity,
                        position: value.Position),
                    FallStarted value => new ObservedEvent(
                        nameof(FallStarted),
                        entity,
                        direction: value.Direction),
                    Landed value => new ObservedEvent(
                        nameof(Landed),
                        entity,
                        direction: value.Direction),
                    PlayerAte value => new ObservedEvent(
                        nameof(PlayerAte),
                        entity,
                        eatenEntity: ResolveAlias(value.EatenEntityId, aliasesById)),
                    PlayerBlocked value => new ObservedEvent(
                        nameof(PlayerBlocked),
                        entity,
                        direction: value.Direction),
                    PlayerMoveStarted value => new ObservedEvent(
                        nameof(PlayerMoveStarted),
                        entity,
                        direction: value.Direction),
                    PlayerReturned value => new ObservedEvent(
                        nameof(PlayerReturned),
                        entity,
                        position: value.Position),
                    PlayerReversed value => new ObservedEvent(
                        nameof(PlayerReversed),
                        entity,
                        direction: value.Direction),
                    PlayerRotationCompleted value => new ObservedEvent(
                        nameof(PlayerRotationCompleted),
                        entity,
                        direction: value.Direction),
                    PlayerRotationStarted value => new ObservedEvent(
                        nameof(PlayerRotationStarted),
                        entity,
                        from: value.From,
                        to: value.To),
                    PlayerStopped => new ObservedEvent(nameof(PlayerStopped), entity),
                    PushStarted value => new ObservedEvent(
                        nameof(PushStarted),
                        entity,
                        direction: value.Direction),
                    SlideStarted value => new ObservedEvent(
                        nameof(SlideStarted),
                        entity,
                        direction: value.Direction),
                    _ => throw new InvalidOperationException(
                        $"Unsupported simulation event type '{simulationEvent.GetType().FullName}'."),
                });
            }

            return result;
        }

        private static string ResolveAlias(
            EntityId id,
            IReadOnlyDictionary<EntityId, string> aliasesById)
        {
            return aliasesById.TryGetValue(id, out string? alias)
                ? alias
                : throw new InvalidOperationException($"Event references unregistered entity '{id}'.");
        }
    }
}
