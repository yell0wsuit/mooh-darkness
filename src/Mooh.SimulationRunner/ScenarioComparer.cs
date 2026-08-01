using System;
using System.Collections.Generic;

using Mooh.Core.Grid;

namespace Mooh.SimulationRunner
{
    /// <summary>Compares normalized simulation observations with scenario expectations.</summary>
    public static class ScenarioComparer
    {
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
