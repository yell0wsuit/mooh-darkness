using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Motion;
using Mooh.Core.Player;

namespace Mooh.SimulationRunner
{
    /// <summary>Compares normalized simulation observations with scenario expectations.</summary>
    public static class ScenarioComparer
    {
        private const double FloatingTolerance = 1e-10;

        /// <summary>Compares one complete ordered event sequence.</summary>
        /// <param name="expected">The complete expected event sequence.</param>
        /// <param name="actual">The complete observed event sequence.</param>
        /// <param name="source">The scenario source name.</param>
        /// <param name="stepIndex">The zero-based simulation step index.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="expected"/>, <paramref name="actual"/>, or <paramref name="source"/>
        /// is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ScenarioMismatchException">The sequences differ.</exception>
        public static void CompareEvents(
            IReadOnlyList<ExpectedEvent> expected,
            IReadOnlyList<ObservedEvent> actual,
            string source,
            int stepIndex)
        {
            ArgumentNullException.ThrowIfNull(expected);
            ArgumentNullException.ThrowIfNull(actual);
            ArgumentNullException.ThrowIfNull(source);
            string path = $"steps[{stepIndex}].expectEvents";
            if (expected.Count != actual.Count)
            {
                throw new ScenarioMismatchException(
                    source,
                    path,
                    $"expected {expected.Count} event(s), actual {actual.Count}.");
            }

            for (int index = 0; index < expected.Count; index++)
            {
                ObservedEvent normalizedExpected = Normalize(expected[index]);
                if (normalizedExpected != actual[index])
                {
                    throw new ScenarioMismatchException(
                        source,
                        $"{path}[{index}]",
                        $"expected {normalizedExpected}, actual {actual[index]}.");
                }
            }
        }

        /// <summary>Compares the exact surviving roster and observable final entity state.</summary>
        /// <param name="expected">The expected final state declaration.</param>
        /// <param name="materialized">The live scenario after all steps completed.</param>
        /// <param name="source">The scenario source name.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="expected"/>, <paramref name="materialized"/>, or <paramref name="source"/>
        /// is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ScenarioMismatchException">The final state differs.</exception>
        public static void CompareFinalState(
            ExpectedFinalState expected,
            MaterializedScenario materialized,
            string source)
        {
            ArgumentNullException.ThrowIfNull(expected);
            ArgumentNullException.ThrowIfNull(materialized);
            ArgumentNullException.ThrowIfNull(source);

            ObservedEntityState[] actual =
            [
                .. materialized.World.Entities.Select(
                    entity => ObserveEntity(entity, materialized.AliasesById[entity.Id])),
            ];
            const string path = "expectFinal.entities";
            if (expected.Entities.Count != actual.Length)
            {
                throw new ScenarioMismatchException(
                    source,
                    path,
                    $"expected {expected.Entities.Count} surviving entity(ies), actual {actual.Length}.");
            }

            for (int index = 0; index < expected.Entities.Count; index++)
            {
                CompareEntity(expected.Entities[index], actual[index], source, $"{path}[{index}]");
            }
        }

        private static void CompareEntity(
            ExpectedEntityState expected,
            ObservedEntityState actual,
            string source,
            string path)
        {
            CompareValue(expected.Alias, actual.Alias, source, $"{path}.alias");
            CompareValue(
                new GridPosition(expected.Position.X, expected.Position.Y),
                actual.Position,
                source,
                $"{path}.position");
            CompareValue(
                ParseOptionalDirection(expected.FacingDirection),
                actual.FacingDirection,
                source,
                $"{path}.facingDirection");
            CompareMotion(expected.Motion, actual.Motion, source, $"{path}.motion");
            CompareRotation(expected.Rotation, actual.Rotation, source, $"{path}.rotation");
        }

        private static void CompareMotion(
            ExpectedMotionState? expected,
            ObservedMotionState? actual,
            string source,
            string path)
        {
            if (expected is null || actual is null)
            {
                if (expected is not null || actual is not null)
                {
                    throw Mismatch(source, path, expected, actual);
                }

                return;
            }

            CompareValue(
                Enum.Parse<MotionCause>(expected.Cause, ignoreCase: false),
                actual.Cause,
                source,
                $"{path}.cause");
            CompareValue(
                Enum.Parse<Direction>(expected.SegmentDirection, ignoreCase: false),
                actual.SegmentDirection,
                source,
                $"{path}.segmentDirection");
            CompareValue(expected.TravelSign, actual.TravelSign, source, $"{path}.travelSign");
            CompareDouble(expected.Speed, actual.Speed, source, $"{path}.speed");
            CompareDouble(expected.Progress, actual.Progress, source, $"{path}.progress");
            CompareValue(
                expected.ResolutionOrder,
                actual.ResolutionOrder,
                source,
                $"{path}.resolutionOrder");
        }

        private static void CompareRotation(
            ExpectedRotationState? expected,
            ObservedRotationState? actual,
            string source,
            string path)
        {
            if (expected is null || actual is null)
            {
                if (expected is not null || actual is not null)
                {
                    throw Mismatch(source, path, expected, actual);
                }

                return;
            }

            CompareValue(
                Enum.Parse<Direction>(expected.From, ignoreCase: false),
                actual.From,
                source,
                $"{path}.from");
            CompareValue(
                Enum.Parse<Direction>(expected.To, ignoreCase: false),
                actual.To,
                source,
                $"{path}.to");
            CompareDouble(
                expected.RemainingSeconds,
                actual.RemainingSeconds,
                source,
                $"{path}.remainingSeconds");
            if (expected.SuspendedProgress is double expectedProgress
                && actual.SuspendedProgress is double actualProgress)
            {
                CompareDouble(expectedProgress, actualProgress, source, $"{path}.suspendedProgress");
            }
            else
            {
                CompareValue(
                    expected.SuspendedProgress,
                    actual.SuspendedProgress,
                    source,
                    $"{path}.suspendedProgress");
            }
        }

        private static ObservedEntityState ObserveEntity(Entity entity, string alias)
        {
            MotionState? motion = entity.Motion;
            PlayerRotation? rotation = entity.PlayerBody?.Rotation;
            return new ObservedEntityState(
                alias,
                entity.Position,
                motion is null
                    ? null
                    : new ObservedMotionState(
                        motion.Cause,
                        motion.SegmentDirection,
                        motion.TravelSign,
                        motion.Speed,
                        motion.Progress,
                        motion.ResolutionOrder),
                entity.PlayerBody?.FacingDirection,
                rotation is null
                    ? null
                    : new ObservedRotationState(
                        rotation.From,
                        rotation.To,
                        rotation.RemainingSeconds,
                        rotation.SuspendedProgress));
        }

        private static void CompareDouble(
            double expected,
            double actual,
            string source,
            string path)
        {
            if (Math.Abs(expected - actual) > FloatingTolerance)
            {
                throw Mismatch(source, path, expected, actual);
            }
        }

        private static void CompareValue<T>(T expected, T actual, string source, string path)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw Mismatch(source, path, expected, actual);
            }
        }

        private static ScenarioMismatchException Mismatch(
            string source,
            string path,
            object? expected,
            object? actual)
        {
            return new ScenarioMismatchException(
                source,
                path,
                $"expected {expected}, actual {actual}.");
        }

        private static ObservedEvent Normalize(ExpectedEvent expected)
        {
            return new ObservedEvent(
                expected.Type,
                expected.Entity,
                expected.EatenEntity,
                ParseOptionalDirection(expected.Direction),
                ParseOptionalDirection(expected.From),
                ParseOptionalDirection(expected.To),
                expected.Position is null
                    ? null
                    : new GridPosition(expected.Position.X, expected.Position.Y));
        }

        private static Direction? ParseOptionalDirection(string? value)
        {
            return value is null ? null : Enum.Parse<Direction>(value, ignoreCase: false);
        }
    }
}
