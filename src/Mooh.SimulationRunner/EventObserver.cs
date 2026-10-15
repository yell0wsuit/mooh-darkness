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
        /// <exception cref="InvalidOperationException">An event type is unsupported.</exception>
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
                    DamageApplied value => new ObservedEvent(
                        nameof(DamageApplied),
                        entity,
                        sourceEntity: ResolveAlias(value.SourceEntityId, aliasesById),
                        damageKind: value.Kind,
                        amount: value.Amount,
                        remainingHealth: value.RemainingHealth),
                    DamageIgnored value => new ObservedEvent(
                        nameof(DamageIgnored),
                        entity,
                        sourceEntity: ResolveAlias(value.SourceEntityId, aliasesById),
                        damageKind: value.Kind,
                        damageResult: value.Reason),
                    EnemyAttackStarted value => new ObservedEvent(
                        nameof(EnemyAttackStarted),
                        entity,
                        position: value.TargetCell,
                        durationMilliseconds: (long)value.Duration.TotalMilliseconds),
                    EnemyFrozen => new ObservedEvent(nameof(EnemyFrozen), entity),
                    EnemyMoveStarted value => new ObservedEvent(
                        nameof(EnemyMoveStarted),
                        entity,
                        fromPosition: value.From,
                        toPosition: value.To),
                    EnemyStrike value => new ObservedEvent(
                        nameof(EnemyStrike),
                        entity,
                        position: value.TargetCell),
                    EnemyThawed => new ObservedEvent(nameof(EnemyThawed), entity),
                    EnemyTransformationCompleted value => new ObservedEvent(
                        nameof(EnemyTransformationCompleted),
                        entity,
                        isSmall: value.IsSmall),
                    EnemyTransformationStarted value => new ObservedEvent(
                        nameof(EnemyTransformationStarted),
                        entity,
                        durationMilliseconds: (long)value.Duration.TotalMilliseconds,
                        isSmall: value.IsSmall),
                    EnemyTurnCompleted value => new ObservedEvent(
                        nameof(EnemyTurnCompleted),
                        entity,
                        direction: value.FacingDirection),
                    EnemyTurnStarted value => new ObservedEvent(
                        nameof(EnemyTurnStarted),
                        entity,
                        from: value.FromDirection,
                        to: value.ToDirection,
                        durationMilliseconds: (long)value.Duration.TotalMilliseconds),
                    EntityDestroyed value => new ObservedEvent(
                        nameof(EntityDestroyed),
                        entity,
                        sourceEntity: ResolveAlias(value.SourceEntityId, aliasesById)),
                    ExplosionCellReached value => new ObservedEvent(
                        nameof(ExplosionCellReached),
                        entity,
                        position: value.Position),
                    ExplosionStarted value => new ObservedEvent(
                        nameof(ExplosionStarted),
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
                    LevelLost => new ObservedEvent(nameof(LevelLost), entity),
                    LevelOutcomeAnnounced => new ObservedEvent(nameof(LevelOutcomeAnnounced), entity),
                    StrikePushStopped value => new ObservedEvent(nameof(StrikePushStopped), entity, direction: value.Direction),
                    LevelWon => new ObservedEvent(nameof(LevelWon), entity),
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
                    PlayerDied => new ObservedEvent(nameof(PlayerDied), entity),
                    PlayerGrenadePlaced value => new ObservedEvent(
                        nameof(PlayerGrenadePlaced),
                        entity,
                        position: value.Position),
                    PlayerGrenadeThrowStarted value => new ObservedEvent(
                        nameof(PlayerGrenadeThrowStarted),
                        entity,
                        direction: value.Direction,
                        durationMilliseconds: (long)value.Duration.TotalMilliseconds),
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
                    PlayerShockEnded => new ObservedEvent(nameof(PlayerShockEnded), entity),
                    PlayerShockStarted value => new ObservedEvent(
                        nameof(PlayerShockStarted),
                        entity,
                        durationMilliseconds: (long)value.Duration.TotalMilliseconds),
                    PlayerStopped => new ObservedEvent(nameof(PlayerStopped), entity),
                    PlayerStrikeStarted value => new ObservedEvent(
                        nameof(PlayerStrikeStarted),
                        entity,
                        direction: value.Direction,
                        durationMilliseconds: (long)value.Duration.TotalMilliseconds),
                    PlayerStruck value => new ObservedEvent(
                        nameof(PlayerStruck),
                        entity,
                        direction: value.Direction,
                        position: value.Position),
                    PlayerWeaponEmpty => new ObservedEvent(nameof(PlayerWeaponEmpty), entity),
                    PushStarted value => new ObservedEvent(
                        nameof(PushStarted),
                        entity,
                        direction: value.Direction),
                    ShakeImpact => new ObservedEvent(nameof(ShakeImpact), entity),
                    SlideStarted value => new ObservedEvent(
                        nameof(SlideStarted),
                        entity,
                        direction: value.Direction),
                    SmallBeetleSpawned value => new ObservedEvent(
                        nameof(SmallBeetleSpawned),
                        entity,
                        position: value.Position),
                    WorldRotationStarted value => new ObservedEvent(
                        nameof(WorldRotationStarted),
                        entity,
                        durationMilliseconds: (long)value.Duration.TotalMilliseconds,
                        worldRotation: value.Direction),
                    WorldRotationCompleted value => new ObservedEvent(
                        nameof(WorldRotationCompleted),
                        entity,
                        worldRotation: value.Direction,
                        width: value.Width,
                        height: value.Height),
                    _ => throw new InvalidOperationException(
                        $"Unsupported simulation event type '{simulationEvent.GetType().FullName}'."),
                });
            }

            return result;
        }

        internal static string ResolveAlias(
            EntityId id,
            IReadOnlyDictionary<EntityId, string> aliasesById)
        {
            return aliasesById.TryGetValue(id, out string? alias)
                ? alias
                : $"spawned:{id.Value:D}";
        }

        internal static bool IsSpawnedAlias(string alias)
        {
            return alias.StartsWith("spawned:", StringComparison.Ordinal)
                && Guid.TryParseExact(alias.AsSpan(8), "D", out _);
        }
    }
}
