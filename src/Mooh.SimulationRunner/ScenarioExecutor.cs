using System;
using System.Collections.Generic;

using Mooh.Core.Grid;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.SimulationRunner
{
    /// <summary>Executes complete self-verifying scenarios through the core simulation.</summary>
    public static class ScenarioExecutor
    {
        /// <summary>Runs one in-memory scenario and captures validation or expectation failures.</summary>
        /// <param name="scenario">The scenario to execute.</param>
        /// <param name="source">The source name used in diagnostics.</param>
        /// <returns>The scenario outcome.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="scenario"/> or <paramref name="source"/> is <see langword="null"/>.
        /// </exception>
        public static ScenarioRunResult Run(ScenarioDocument scenario, string source)
        {
            ArgumentNullException.ThrowIfNull(scenario);
            ArgumentNullException.ThrowIfNull(source);

            try
            {
                ScenarioValidator.Validate(scenario, source);
                MaterializedScenario materialized = ScenarioMaterializer.Materialize(scenario);
                for (int stepIndex = 0; stepIndex < scenario.Steps.Count; stepIndex++)
                {
                    ScenarioStep step = scenario.Steps[stepIndex];
                    if (materialized.World.Player is not null)
                    {
                        materialized.Simulation.SetPlayerInput(CreateInput(step.Input ?? []));
                    }

                    IReadOnlyList<SimulationEvent> events = materialized.Simulation.Advance(
                        TimeSpan.FromMilliseconds(step.AdvanceMilliseconds));
                    IReadOnlyList<ObservedEvent> observed = EventObserver.Observe(
                        events,
                        materialized.AliasesById);
                    ScenarioComparer.CompareEvents(
                        step.ExpectEvents,
                        observed,
                        source,
                        stepIndex);
                }

                ScenarioComparer.CompareFinalState(scenario.ExpectFinal, materialized, source);
                return new ScenarioRunResult(scenario.Name, source, true, null, string.Empty);
            }
            catch (ScenarioValidationException exception)
            {
                return new ScenarioRunResult(
                    scenario.Name,
                    source,
                    false,
                    ScenarioFailureKind.Validation,
                    exception.Message);
            }
            catch (ScenarioMismatchException exception)
            {
                return new ScenarioRunResult(
                    scenario.Name,
                    source,
                    false,
                    ScenarioFailureKind.Expectation,
                    exception.Message);
            }
        }

        private static PlayerInputState CreateInput(IReadOnlyList<string> directions)
        {
            bool left = false;
            bool up = false;
            bool right = false;
            bool down = false;
            foreach (string value in directions)
            {
                switch (Enum.Parse<Direction>(value, ignoreCase: false))
                {
                    case Direction.Left:
                        left = true;
                        break;
                    case Direction.Up:
                        up = true;
                        break;
                    case Direction.Right:
                        right = true;
                        break;
                    case Direction.Down:
                        down = true;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported input direction '{value}'.");
                }
            }

            return new PlayerInputState(left, up, right, down);
        }
    }
}
