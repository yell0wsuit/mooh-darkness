using System;

using Mooh.Core.Grid;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies exact ordered scenario expectation comparison.</summary>
    public sealed class ScenarioComparerTests
    {
        /// <summary>Verifies an exact event sequence passes.</summary>
        [Fact]
        public void AcceptsExactOrderedEvents()
        {
            ExpectedEvent[] expected =
            [
                new ExpectedEvent
                {
                    Type = "CellEntered",
                    Entity = "rock",
                    Position = new ScenarioPosition { X = 1, Y = 2 },
                },
            ];
            ObservedEvent[] actual =
            [
                new ObservedEvent("CellEntered", "rock", position: new GridPosition(1, 2)),
            ];

            ScenarioComparer.CompareEvents(expected, actual, "exact.json", 0);
        }

        /// <summary>Verifies event count differences identify the failing step.</summary>
        [Fact]
        public void ReportsEventCountMismatch()
        {
            ScenarioMismatchException exception = Assert.Throws<ScenarioMismatchException>(
                () => ScenarioComparer.CompareEvents([], [new ObservedEvent("PlayerStopped", "player")], "count.json", 2));

            Assert.Contains("count.json", exception.Message, StringComparison.Ordinal);
            Assert.Contains("steps[2].expectEvents", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>Verifies the first differing event reports its exact index and values.</summary>
        [Fact]
        public void ReportsFirstEventValueMismatch()
        {
            ExpectedEvent[] expected =
            [
                new ExpectedEvent
                {
                    Type = "CellEntered",
                    Entity = "rock",
                    Position = new ScenarioPosition { X = 1, Y = 2 },
                },
            ];
            ObservedEvent[] actual =
            [
                new ObservedEvent("CellEntered", "rock", position: new GridPosition(2, 2)),
            ];

            ScenarioMismatchException exception = Assert.Throws<ScenarioMismatchException>(
                () => ScenarioComparer.CompareEvents(expected, actual, "position.json", 1));

            Assert.Contains("steps[1].expectEvents[0]", exception.Message, StringComparison.Ordinal);
            Assert.Contains("expected", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("actual", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
