using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Motion;

namespace Mooh.SimulationRunner
{
    /// <summary>Validates scenario schema and cross-reference rules before execution.</summary>
    public static class ScenarioValidator
    {
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
                if (entity.GravityBody is ScenarioGravityBody gravityBody
                    && (!double.IsFinite(gravityBody.FallSpeed) || gravityBody.FallSpeed <= 0))
                {
                    throw Invalid(source, $"{path}.gravityBody.fallSpeed", "must be finite and positive.");
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
                if (step.AdvanceMilliseconds <= 0)
                {
                    throw Invalid(source, $"{path}.advanceMilliseconds", "must be positive.");
                }

                HashSet<Direction> inputs = [];
                List<string> declaredInput = step.Input ?? [];
                for (int inputIndex = 0; inputIndex < declaredInput.Count; inputIndex++)
                {
                    Direction direction = ParseEnum<Direction>(
                        declaredInput[inputIndex],
                        source,
                        $"{path}.input[{inputIndex}]");
                    if (!inputs.Add(direction))
                    {
                        throw Invalid(source, $"{path}.input[{inputIndex}]", "duplicates a held direction.");
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

            ValidateAliasReference(expected.Entity, aliases, source, $"{path}.entity");
            if (expected.EatenEntity is string eatenEntity)
            {
                ValidateAliasReference(eatenEntity, aliases, source, $"{path}.eatenEntity");
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
                ValidatePosition(expected.Position, world, source, $"{path}.position");
            }
        }

        private static readonly string[] SupportedEventTypes =
        [
            "CellEntered",
            "FallStarted",
            "Landed",
            "PlayerAte",
            "PlayerBlocked",
            "PlayerMoveStarted",
            "PlayerReturned",
            "PlayerReversed",
            "PlayerRotationCompleted",
            "PlayerRotationStarted",
            "PlayerStopped",
            "PushStarted",
            "SlideStarted",
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

                ValidatePosition(entity.Position, scenario.World, source, $"{path}.position");
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

        private static void ValidatePosition(
            ScenarioPosition position,
            ScenarioWorld world,
            string source,
            string path)
        {
            if (position is null)
            {
                throw Invalid(source, path, "is required.");
            }

            if (position.X < 0 || position.X >= world.Width
                || position.Y < 0 || position.Y >= world.Height)
            {
                throw Invalid(source, path, "is outside the world.");
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
                ? parsed
                : throw Invalid(source, path, $"has unsupported value '{value}'.");
        }

        private static ScenarioValidationException Invalid(string source, string path, string message)
        {
            return new ScenarioValidationException(source, $"{path} {message}");
        }
    }
}
