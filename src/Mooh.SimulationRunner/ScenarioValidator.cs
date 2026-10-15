using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Enemies;
using Mooh.Core.Explosions;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;
using Mooh.Core.Player;

namespace Mooh.SimulationRunner
{
    /// <summary>Validates scenario schema and cross-reference rules before execution.</summary>
    public static class ScenarioValidator
    {
        private static readonly long MaximumAdvanceMilliseconds =
            TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerMillisecond;

        [Flags]
        private enum EventFields
        {
            None = 0,
            EatenEntity = 1 << 0,
            Direction = 1 << 1,
            From = 1 << 2,
            To = 1 << 3,
            Position = 1 << 4,
            FromPosition = 1 << 5,
            ToPosition = 1 << 6,
            SourceEntity = 1 << 7,
            DamageKind = 1 << 8,
            DamageResult = 1 << 9,
            Amount = 1 << 10,
            RemainingHealth = 1 << 11,
            DurationMilliseconds = 1 << 12,
            IsSmall = 1 << 13,
            WorldRotation = 1 << 14,
            Width = 1 << 15,
            Height = 1 << 16,
        }

        /// <summary>Validates a scenario document.</summary>
        /// <param name="scenario">The scenario to validate.</param>
        /// <param name="source">The source name used in diagnostics.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="scenario"/> or <paramref name="source"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ScenarioValidationException">The scenario is invalid.</exception>
        public static void Validate(ScenarioDocument scenario, string source)
        {
            ArgumentNullException.ThrowIfNull(scenario);
            ArgumentNullException.ThrowIfNull(source);

            if (scenario.FormatVersion != 1)
            {
                throw Invalid(source, "formatVersion", "must be 1.");
            }

            if (string.IsNullOrWhiteSpace(scenario.Name))
            {
                throw Invalid(source, "name", "must not be blank.");
            }

            ValidateWorld(scenario.World, source);
            ValidateSteps(scenario, source);
            ValidateFinalState(scenario, source);
        }

        private static void ValidateWorld(ScenarioWorld world, string source)
        {
            if (world is null)
            {
                throw Invalid(source, "world", "is required.");
            }

            if (world.Width <= 0)
            {
                throw Invalid(source, "world.width", "must be positive.");
            }

            if (world.Height <= 0)
            {
                throw Invalid(source, "world.height", "must be positive.");
            }

            _ = ParseEnum<Direction>(world.Gravity, source, "world.gravity");
            if (world.HealthDrainPerSecond is double drain && (!double.IsFinite(drain) || drain < 0))
            {
                throw Invalid(source, "world.healthDrainPerSecond", "must be finite and non-negative.");
            }

            if (world.Entities is null || world.Entities.Count == 0)
            {
                throw Invalid(source, "world.entities", "must contain at least one entity.");
            }

            HashSet<string> aliases = [];
            int playerCount = 0;
            for (int index = 0; index < world.Entities.Count; index++)
            {
                ScenarioEntity entity = world.Entities[index]
                    ?? throw Invalid(source, $"world.entities[{index}]", "must be an object.");
                string path = $"world.entities[{index}]";
                if (string.IsNullOrWhiteSpace(entity.Alias))
                {
                    throw Invalid(source, $"{path}.alias", "must not be blank.");
                }

                if (!aliases.Add(entity.Alias))
                {
                    throw Invalid(source, $"{path}.alias", $"duplicates '{entity.Alias}'.");
                }

                ValidatePosition(entity.Position, world, source, $"{path}.position");
                ValidateInteractions(entity.Interactions, source, $"{path}.interactions");
                if (entity.GreenMonsterBody is not null && entity.BlueMonsterBody is not null)
                {
                    throw Invalid(
                        source,
                        path,
                        "cannot combine green and blue monster bodies.");
                }

                if (entity.GravityBody is ScenarioGravityBody gravityBody
                    && (!double.IsFinite(gravityBody.FallSpeed) || gravityBody.FallSpeed <= 0))
                {
                    throw Invalid(source, $"{path}.gravityBody.fallSpeed", "must be finite and positive.");
                }

                if (entity.ExplosionBody is ScenarioExplosionBody explosionBody)
                {
                    _ = ParseEnum<ExplosionReaction>(
                        explosionBody.Reaction,
                        source,
                        $"{path}.explosionBody.reaction");
                }

                if (entity.WorldRotationBody is ScenarioWorldRotationBody worldRotationBody)
                {
                    _ = ParseEnum<WorldRotationDirection>(
                        worldRotationBody.Direction,
                        source,
                        $"{path}.worldRotationBody.direction");
                }

                if (entity.PlayerBody is ScenarioPlayerBody playerBody)
                {
                    playerCount++;
                    if (!double.IsFinite(playerBody.MoveSpeed) || playerBody.MoveSpeed <= 0)
                    {
                        throw Invalid(source, $"{path}.playerBody.moveSpeed", "must be finite and positive.");
                    }

                    if (!double.IsFinite(playerBody.PushSpeedMultiplier)
                        || playerBody.PushSpeedMultiplier <= 0)
                    {
                        throw Invalid(
                            source,
                            $"{path}.playerBody.pushSpeedMultiplier",
                            "must be finite and positive.");
                    }

                    _ = ParseEnum<Direction>(
                        playerBody.FacingDirection,
                        source,
                        $"{path}.playerBody.facingDirection");
                }

                if (entity.HealthBody is ScenarioHealthBody healthBody)
                {
                    if (!double.IsFinite(healthBody.Maximum) || healthBody.Maximum <= 0)
                    {
                        throw Invalid(source, $"{path}.healthBody.maximum", "must be finite and positive.");
                    }

                    if (!double.IsFinite(healthBody.Current)
                        || healthBody.Current <= 0
                        || healthBody.Current > healthBody.Maximum)
                    {
                        throw Invalid(
                            source,
                            $"{path}.healthBody.current",
                            "must be finite and within (0, maximum].");
                    }

                    ValidateNonNegativeMilliseconds(
                        healthBody.ImmunityMilliseconds,
                        source,
                        $"{path}.healthBody.immunityMilliseconds");
                }

                if (entity.GreenMonsterBody is ScenarioGreenMonsterBody monsterBody)
                {
                    if (!double.IsFinite(monsterBody.MoveSpeed) || monsterBody.MoveSpeed <= 0)
                    {
                        throw Invalid(source, $"{path}.greenMonsterBody.moveSpeed", "must be finite and positive.");
                    }

                    _ = ParseEnum<Direction>(
                        monsterBody.FacingDirection,
                        source,
                        $"{path}.greenMonsterBody.facingDirection");
                    ValidatePositiveMilliseconds(
                        monsterBody.TurnMilliseconds,
                        source,
                        $"{path}.greenMonsterBody.turnMilliseconds");
                    ValidatePositiveMilliseconds(
                        monsterBody.AttackWindupMilliseconds,
                        source,
                        $"{path}.greenMonsterBody.attackWindupMilliseconds");
                    ValidatePositiveMilliseconds(
                        monsterBody.AttackRecoveryMilliseconds,
                        source,
                        $"{path}.greenMonsterBody.attackRecoveryMilliseconds");
                    if (!double.IsFinite(monsterBody.StrikeDamage) || monsterBody.StrikeDamage <= 0)
                    {
                        throw Invalid(source, $"{path}.greenMonsterBody.strikeDamage", "must be finite and positive.");
                    }
                }

                if (entity.BlueMonsterBody is ScenarioBlueMonsterBody blueMonsterBody)
                {
                    if (!double.IsFinite(blueMonsterBody.MoveSpeed) || blueMonsterBody.MoveSpeed <= 0)
                    {
                        throw Invalid(source, $"{path}.blueMonsterBody.moveSpeed", "must be finite and positive.");
                    }

                    _ = ParseEnum<Direction>(
                        blueMonsterBody.FacingDirection,
                        source,
                        $"{path}.blueMonsterBody.facingDirection");
                    ValidatePositiveMilliseconds(
                        blueMonsterBody.TurnMilliseconds,
                        source,
                        $"{path}.blueMonsterBody.turnMilliseconds");
                    ValidatePositiveMilliseconds(
                        blueMonsterBody.AttackWindupMilliseconds,
                        source,
                        $"{path}.blueMonsterBody.attackWindupMilliseconds");
                    ValidatePositiveMilliseconds(
                        blueMonsterBody.LeftAttackRecoveryMilliseconds,
                        source,
                        $"{path}.blueMonsterBody.leftAttackRecoveryMilliseconds");
                    if (blueMonsterBody.LeftAttackRecoveryMilliseconds
                        <= blueMonsterBody.TurnMilliseconds)
                    {
                        throw Invalid(
                            source,
                            $"{path}.blueMonsterBody.leftAttackRecoveryMilliseconds",
                            "must exceed turnMilliseconds.");
                    }

                    ValidatePositiveMilliseconds(
                        blueMonsterBody.AttackRecoveryMilliseconds,
                        source,
                        $"{path}.blueMonsterBody.attackRecoveryMilliseconds");
                    if (blueMonsterBody.AttackRecoveryMilliseconds
                        <= blueMonsterBody.TurnMilliseconds)
                    {
                        throw Invalid(
                            source,
                            $"{path}.blueMonsterBody.attackRecoveryMilliseconds",
                            "must exceed turnMilliseconds.");
                    }

                    if (!double.IsFinite(blueMonsterBody.StrikeDamage)
                        || blueMonsterBody.StrikeDamage <= 0)
                    {
                        throw Invalid(
                            source,
                            $"{path}.blueMonsterBody.strikeDamage",
                            "must be finite and positive.");
                    }
                }
            }

            if (playerCount > 1)
            {
                throw Invalid(source, "world.entities", "must contain at most one player.");
            }
        }

        private static void ValidateSteps(ScenarioDocument scenario, string source)
        {
            if (scenario.Steps is null || scenario.Steps.Count == 0)
            {
                throw Invalid(source, "steps", "must contain at least one step.");
            }

            HashSet<string> aliases = scenario.World.Entities
                .Select(entity => entity.Alias)
                .ToHashSet(StringComparer.Ordinal);
            bool hasPlayer = scenario.World.Entities.Any(entity => entity.PlayerBody is not null);
            for (int stepIndex = 0; stepIndex < scenario.Steps.Count; stepIndex++)
            {
                ScenarioStep step = scenario.Steps[stepIndex]
                    ?? throw Invalid(source, $"steps[{stepIndex}]", "must be an object.");
                string path = $"steps[{stepIndex}]";
                if (step.AdvanceMilliseconds <= 0
                    || step.AdvanceMilliseconds > MaximumAdvanceMilliseconds)
                {
                    throw Invalid(
                        source,
                        $"{path}.advanceMilliseconds",
                        $"must be within [1, {MaximumAdvanceMilliseconds}].");
                }

                HashSet<string> inputs = [];
                List<string> declaredInput = step.Input ?? [];
                for (int inputIndex = 0; inputIndex < declaredInput.Count; inputIndex++)
                {
                    string held = declaredInput[inputIndex];
                    if (!string.Equals(held, ScenarioExecutor.FireInput, StringComparison.Ordinal))
                    {
                        held = ParseEnum<Direction>(
                            held,
                            source,
                            $"{path}.input[{inputIndex}]").ToString();
                    }

                    if (!inputs.Add(held))
                    {
                        throw Invalid(source, $"{path}.input[{inputIndex}]", "duplicates a held control.");
                    }
                }

                if (inputs.Count != 0 && !hasPlayer)
                {
                    throw Invalid(source, $"{path}.input", "requires a player entity.");
                }

                if (step.ExpectEvents is null)
                {
                    throw Invalid(source, $"{path}.expectEvents", "is required.");
                }

                for (int eventIndex = 0; eventIndex < step.ExpectEvents.Count; eventIndex++)
                {
                    ValidateEvent(
                        step.ExpectEvents[eventIndex],
                        aliases,
                        scenario.World,
                        source,
                        $"{path}.expectEvents[{eventIndex}]");
                }
            }
        }

        private static void ValidateEvent(
            ExpectedEvent expected,
            IReadOnlySet<string> aliases,
            ScenarioWorld world,
            string source,
            string path)
        {
            if (expected is null)
            {
                throw Invalid(source, path, "must be an object.");
            }

            if (!SupportedEventTypes.Contains(expected.Type, StringComparer.Ordinal))
            {
                throw Invalid(source, $"{path}.type", $"has unsupported value '{expected.Type}'.");
            }

            ValidateEventFields(expected, source, path);
            ValidateAliasReference(expected.Entity, aliases, source, $"{path}.entity");
            if (expected.EatenEntity is string eatenEntity)
            {
                ValidateAliasReference(eatenEntity, aliases, source, $"{path}.eatenEntity");
            }

            if (expected.SourceEntity is string sourceEntity)
            {
                ValidateAliasReference(sourceEntity, aliases, source, $"{path}.sourceEntity");
            }

            if (expected.Direction is string direction)
            {
                _ = ParseEnum<Direction>(direction, source, $"{path}.direction");
            }

            if (expected.From is string from)
            {
                _ = ParseEnum<Direction>(from, source, $"{path}.from");
            }

            if (expected.To is string to)
            {
                _ = ParseEnum<Direction>(to, source, $"{path}.to");
            }

            if (expected.Position is not null)
            {
                ValidatePosition(expected.Position, world, source, $"{path}.position", allowRotatedBounds: true);
            }

            if (expected.FromPosition is not null)
            {
                ValidatePosition(expected.FromPosition, world, source, $"{path}.fromPosition", allowRotatedBounds: true);
            }

            if (expected.ToPosition is not null)
            {
                ValidatePosition(expected.ToPosition, world, source, $"{path}.toPosition", allowRotatedBounds: true);
            }

            if (expected.DamageKind is string damageKind)
            {
                _ = ParseEnum<DamageKind>(damageKind, source, $"{path}.damageKind");
            }

            if (expected.DamageResult is string damageResult)
            {
                _ = ParseEnum<DamageResult>(damageResult, source, $"{path}.damageResult");
            }

            if (expected.Amount is double amount && (!double.IsFinite(amount) || amount <= 0))
            {
                throw Invalid(source, $"{path}.amount", "must be finite and positive.");
            }

            if (expected.RemainingHealth is double remainingHealth
                && (!double.IsFinite(remainingHealth) || remainingHealth < 0))
            {
                throw Invalid(source, $"{path}.remainingHealth", "must be finite and non-negative.");
            }

            if (expected.DurationMilliseconds is long durationMilliseconds)
            {
                ValidatePositiveMilliseconds(durationMilliseconds, source, $"{path}.durationMilliseconds");
            }
        }

        private static void ValidateEventFields(ExpectedEvent expected, string source, string path)
        {
            EventFields required = expected.Type switch
            {
                "CellEntered" or "PlayerReturned" => EventFields.Position,
                "FallStarted" or "Landed" or "PlayerBlocked" or "PlayerMoveStarted"
                    or "PlayerReversed" or "PlayerRotationCompleted" or "PushStarted"
                    or "SlideStarted" => EventFields.Direction,
                "PlayerAte" => EventFields.EatenEntity,
                "PlayerGrenadeThrowStarted" or "PlayerStrikeStarted" => EventFields.Direction
                    | EventFields.DurationMilliseconds,
                "PlayerGrenadePlaced" => EventFields.Position,
                "PlayerStruck" => EventFields.Direction | EventFields.Position,
                "PlayerRotationStarted" => EventFields.From | EventFields.To,
                "DamageApplied" => EventFields.SourceEntity | EventFields.DamageKind
                    | EventFields.Amount | EventFields.RemainingHealth,
                "DamageIgnored" => EventFields.SourceEntity | EventFields.DamageKind
                    | EventFields.DamageResult,
                "EntityDestroyed" => EventFields.SourceEntity,
                "EnemyAttackStarted" => EventFields.Position | EventFields.DurationMilliseconds,
                "EnemyMoveStarted" => EventFields.FromPosition | EventFields.ToPosition,
                "EnemyStrike" => EventFields.Position,
                "EnemyTurnCompleted" => EventFields.Direction,
                "EnemyTurnStarted" => EventFields.From | EventFields.To | EventFields.DurationMilliseconds,
                "EnemyTransformationStarted" => EventFields.DurationMilliseconds | EventFields.IsSmall,
                "EnemyTransformationCompleted" => EventFields.IsSmall,
                "ExplosionCellReached" or "ExplosionStarted" => EventFields.Position,
                "SmallBeetleSpawned" => EventFields.Position,
                "WorldRotationStarted" => EventFields.WorldRotation
                    | EventFields.DurationMilliseconds,
                "WorldRotationCompleted" => EventFields.WorldRotation | EventFields.Width
                    | EventFields.Height,
                "PlayerShockStarted" => EventFields.DurationMilliseconds,
                "LevelLost" or "LevelOutcomeAnnounced" or "LevelWon" => EventFields.None,
                "PlayerDied" or "PlayerShockEnded" or "PlayerStopped"
                    or "PlayerWeaponEmpty" => EventFields.None,
                _ => EventFields.None,
            };
            EventFields actual = EventFields.None;
            actual |= expected.EatenEntity is null ? EventFields.None : EventFields.EatenEntity;
            actual |= expected.Direction is null ? EventFields.None : EventFields.Direction;
            actual |= expected.From is null ? EventFields.None : EventFields.From;
            actual |= expected.To is null ? EventFields.None : EventFields.To;
            actual |= expected.Position is null ? EventFields.None : EventFields.Position;
            actual |= expected.FromPosition is null ? EventFields.None : EventFields.FromPosition;
            actual |= expected.ToPosition is null ? EventFields.None : EventFields.ToPosition;
            actual |= expected.SourceEntity is null ? EventFields.None : EventFields.SourceEntity;
            actual |= expected.DamageKind is null ? EventFields.None : EventFields.DamageKind;
            actual |= expected.DamageResult is null ? EventFields.None : EventFields.DamageResult;
            actual |= expected.Amount is null ? EventFields.None : EventFields.Amount;
            actual |= expected.RemainingHealth is null ? EventFields.None : EventFields.RemainingHealth;
            actual |= expected.DurationMilliseconds is null
                ? EventFields.None
                : EventFields.DurationMilliseconds;
            actual |= expected.IsSmall is null ? EventFields.None : EventFields.IsSmall;
            actual |= expected.WorldRotation is null ? EventFields.None : EventFields.WorldRotation;
            actual |= expected.Width is null ? EventFields.None : EventFields.Width;
            actual |= expected.Height is null ? EventFields.None : EventFields.Height;
            if (actual != required)
            {
                throw Invalid(
                    source,
                    path,
                    $"event type '{expected.Type}' requires exactly fields '{required}'.");
            }
        }

        private static readonly string[] SupportedEventTypes =
        [
            "CellEntered",
            "DamageApplied",
            "DamageIgnored",
            "EnemyAttackStarted",
            "EnemyMoveStarted",
            "EnemyStrike",
            "EnemyFrozen",
            "EnemyThawed",
            "EnemyTransformationCompleted",
            "EnemyTransformationStarted",
            "EnemyTurnCompleted",
            "EnemyTurnStarted",
            "EntityDestroyed",
            "ExplosionCellReached",
            "ExplosionStarted",
            "FallStarted",
            "Landed",
            "LevelLost",
            "LevelOutcomeAnnounced",
            "LevelWon",
            "PlayerAte",
            "PlayerBlocked",
            "PlayerMoveStarted",
            "PlayerDied",
            "PlayerGrenadePlaced",
            "PlayerGrenadeThrowStarted",
            "PlayerReturned",
            "PlayerReversed",
            "PlayerRotationCompleted",
            "PlayerRotationStarted",
            "PlayerShockEnded",
            "PlayerShockStarted",
            "PlayerStopped",
            "PlayerStrikeStarted",
            "PlayerStruck",
            "PlayerWeaponEmpty",
            "PushStarted",
            "SlideStarted",
            "SmallBeetleSpawned",
            "WorldRotationCompleted",
            "WorldRotationStarted",
        ];

        private static void ValidateFinalState(ScenarioDocument scenario, string source)
        {
            if (scenario.ExpectFinal?.Entities is null)
            {
                throw Invalid(source, "expectFinal.entities", "is required.");
            }

            HashSet<string> aliases = scenario.World.Entities
                .Select(entity => entity.Alias)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> expectedAliases = [];
            for (int index = 0; index < scenario.ExpectFinal.Entities.Count; index++)
            {
                ExpectedEntityState entity = scenario.ExpectFinal.Entities[index]
                    ?? throw Invalid(source, $"expectFinal.entities[{index}]", "must be an object.");
                string path = $"expectFinal.entities[{index}]";
                ValidateAliasReference(entity.Alias, aliases, source, $"{path}.alias");
                if (!expectedAliases.Add(entity.Alias))
                {
                    throw Invalid(source, $"{path}.alias", $"duplicates '{entity.Alias}'.");
                }

                ValidatePosition(
                    entity.Position,
                    scenario.World,
                    source,
                    $"{path}.position",
                    allowRotatedBounds: true);
                if (entity.FacingDirection is string facingDirection)
                {
                    _ = ParseEnum<Direction>(facingDirection, source, $"{path}.facingDirection");
                }

                if (entity.Motion is ExpectedMotionState motion)
                {
                    _ = ParseEnum<MotionCause>(motion.Cause, source, $"{path}.motion.cause");
                    _ = ParseEnum<Direction>(
                        motion.SegmentDirection,
                        source,
                        $"{path}.motion.segmentDirection");
                    if (motion.TravelSign is not (-1 or 1))
                    {
                        throw Invalid(source, $"{path}.motion.travelSign", "must be -1 or 1.");
                    }

                    if (!double.IsFinite(motion.Speed) || motion.Speed <= 0)
                    {
                        throw Invalid(source, $"{path}.motion.speed", "must be finite and positive.");
                    }

                    if (!double.IsFinite(motion.Progress) || motion.Progress < 0 || motion.Progress > 1)
                    {
                        throw Invalid(source, $"{path}.motion.progress", "must be finite and within [0, 1].");
                    }
                }

                if (entity.Rotation is ExpectedRotationState rotation)
                {
                    _ = ParseEnum<Direction>(rotation.From, source, $"{path}.rotation.from");
                    _ = ParseEnum<Direction>(rotation.To, source, $"{path}.rotation.to");
                    if (!double.IsFinite(rotation.RemainingSeconds) || rotation.RemainingSeconds < 0)
                    {
                        throw Invalid(
                            source,
                            $"{path}.rotation.remainingSeconds",
                            "must be finite and non-negative.");
                    }
                }

                ScenarioEntity declaration = scenario.World.Entities.Single(
                    candidate => string.Equals(candidate.Alias, entity.Alias, StringComparison.Ordinal));
                ValidateFinalGameplayState(entity, declaration, source, path);
            }
        }

        private static void ValidateFinalGameplayState(
            ExpectedEntityState expected,
            ScenarioEntity declaration,
            string source,
            string path)
        {
            if (declaration.HealthBody is not null)
            {
                if (expected.CurrentHealth is not double currentHealth
                    || !double.IsFinite(currentHealth)
                    || currentHealth < 0
                    || currentHealth > declaration.HealthBody.Maximum)
                {
                    throw Invalid(source, $"{path}.currentHealth", "must be finite and within [0, maximum].");
                }

                ValidateNonNegativeMilliseconds(
                    expected.ImmunityMilliseconds,
                    source,
                    $"{path}.immunityMilliseconds");
            }
            else if (expected.CurrentHealth is not null || expected.ImmunityMilliseconds is not null)
            {
                throw Invalid(source, path, "cannot declare health state without a health body.");
            }

            if (declaration.PlayerBody is not null)
            {
                _ = ParseEnum<Direction>(
                    expected.FacingDirection ?? string.Empty,
                    source,
                    $"{path}.facingDirection");
                _ = ParseEnum<PlayerReactionKind>(
                    expected.PlayerReaction ?? string.Empty,
                    source,
                    $"{path}.playerReaction");
                ValidateNonNegativeMilliseconds(
                    expected.ReactionMilliseconds,
                    source,
                    $"{path}.reactionMilliseconds");
            }
            else if (expected.FacingDirection is not null
                || expected.Rotation is not null
                || expected.PlayerReaction is not null
                || expected.ReactionMilliseconds is not null)
            {
                throw Invalid(source, path, "cannot declare player state without a player body.");
            }

            if (declaration.GreenMonsterBody is not null)
            {
                _ = ParseEnum<GreenMonsterState>(
                    expected.GreenMonsterState ?? string.Empty,
                    source,
                    $"{path}.greenMonsterState");
                _ = ParseEnum<Direction>(
                    expected.GreenMonsterFacingDirection ?? string.Empty,
                    source,
                    $"{path}.greenMonsterFacingDirection");
                ValidateNonNegativeMilliseconds(
                    expected.GreenMonsterRemainingMilliseconds,
                    source,
                    $"{path}.greenMonsterRemainingMilliseconds");
            }
            else if (expected.GreenMonsterState is not null
                || expected.GreenMonsterFacingDirection is not null
                || expected.GreenMonsterRemainingMilliseconds is not null
                || expected.GreenMonsterIsFrozen is not null
                || expected.GreenMonsterIsSmall is not null)
            {
                throw Invalid(source, path, "cannot declare green-monster state without a green-monster body.");
            }

            if (declaration.BlueMonsterBody is not null)
            {
                _ = ParseEnum<BlueMonsterState>(
                    expected.BlueMonsterState ?? string.Empty,
                    source,
                    $"{path}.blueMonsterState");
                _ = ParseEnum<Direction>(
                    expected.BlueMonsterFacingDirection ?? string.Empty,
                    source,
                    $"{path}.blueMonsterFacingDirection");
                ValidateNonNegativeMilliseconds(
                    expected.BlueMonsterRemainingMilliseconds,
                    source,
                    $"{path}.blueMonsterRemainingMilliseconds");
            }
            else if (expected.BlueMonsterState is not null
                || expected.BlueMonsterFacingDirection is not null
                || expected.BlueMonsterRemainingMilliseconds is not null)
            {
                throw Invalid(source, path, "cannot declare blue-monster state without a blue-monster body.");
            }
        }

        private static void ValidateInteractions(
            List<string> interactions,
            string source,
            string path)
        {
            if (interactions is null)
            {
                throw Invalid(source, path, "is required.");
            }

            HashSet<InteractionMask> parsed = [];
            for (int index = 0; index < interactions.Count; index++)
            {
                InteractionMask interaction = ParseEnum<InteractionMask>(
                    interactions[index],
                    source,
                    $"{path}[{index}]");
                if (interaction == InteractionMask.None)
                {
                    throw Invalid(source, $"{path}[{index}]", "must omit 'None' and use an empty array.");
                }

                if (!parsed.Add(interaction))
                {
                    throw Invalid(source, $"{path}[{index}]", "duplicates an interaction.");
                }
            }
        }

        /// <summary>
        /// Checks a declared cell against the world bounds.
        /// </summary>
        /// <remarks>
        /// A world rotation swaps the world's width and height, so a scenario that declares a
        /// rotator has two sets of bounds and which one applies depends on how many turns have
        /// happened by that point in the run. Positions the scenario expects — event cells and
        /// final positions — are therefore accepted in either, while the initial roster is always
        /// checked strictly against the declared world.
        /// </remarks>
        private static void ValidatePosition(
            ScenarioPosition position,
            ScenarioWorld world,
            string source,
            string path,
            bool allowRotatedBounds = false)
        {
            if (position is null)
            {
                throw Invalid(source, path, "is required.");
            }

            bool inDeclared = position.X >= 0 && position.X < world.Width
                && position.Y >= 0 && position.Y < world.Height;
            bool inRotated = allowRotatedBounds
                && HasRotator(world)
                && position.X >= 0 && position.X < world.Height
                && position.Y >= 0 && position.Y < world.Width;
            if (!inDeclared && !inRotated)
            {
                throw Invalid(source, path, "is outside the world.");
            }
        }

        private static bool HasRotator(ScenarioWorld world)
        {
            return world.Entities.Exists(entity => entity.WorldRotationBody is not null);
        }

        private static void ValidatePositiveMilliseconds(long value, string source, string path)
        {
            if (value <= 0 || value > MaximumAdvanceMilliseconds)
            {
                throw Invalid(source, path, $"must be within [1, {MaximumAdvanceMilliseconds}].");
            }
        }

        private static void ValidateNonNegativeMilliseconds(long? value, string source, string path)
        {
            if (value is not long milliseconds
                || milliseconds < 0
                || milliseconds > MaximumAdvanceMilliseconds)
            {
                throw Invalid(source, path, $"must be within [0, {MaximumAdvanceMilliseconds}].");
            }
        }

        private static void ValidateNonNegativeMilliseconds(long value, string source, string path)
        {
            if (value < 0 || value > MaximumAdvanceMilliseconds)
            {
                throw Invalid(source, path, $"must be within [0, {MaximumAdvanceMilliseconds}].");
            }
        }

        private static void ValidateAliasReference(
            string alias,
            IReadOnlySet<string> aliases,
            string source,
            string path)
        {
            if (string.IsNullOrWhiteSpace(alias) || !aliases.Contains(alias))
            {
                throw Invalid(source, path, $"references unknown entity '{alias}'.");
            }
        }

        internal static TEnum ParseEnum<TEnum>(string value, string source, string path)
            where TEnum : struct, Enum
        {
            return !string.IsNullOrWhiteSpace(value)
                && Enum.TryParse(value, ignoreCase: false, out TEnum parsed)
                && Enum.IsDefined(parsed)
                && string.Equals(Enum.GetName(parsed), value, StringComparison.Ordinal)
                ? parsed
                : throw Invalid(source, path, $"has unsupported value '{value}'.");
        }

        private static ScenarioValidationException Invalid(string source, string path, string message)
        {
            return new ScenarioValidationException(source, $"{path} {message}");
        }
    }
}
