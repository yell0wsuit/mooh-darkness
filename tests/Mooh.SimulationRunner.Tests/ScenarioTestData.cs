using System.Collections.Generic;

namespace Mooh.SimulationRunner.Tests
{
    internal static class ScenarioTestData
    {
        internal static ScenarioDocument CreateValid()
        {
            return new ScenarioDocument
            {
                FormatVersion = 1,
                Name = "valid",
                World = new ScenarioWorld
                {
                    Width = 4,
                    Height = 4,
                    Gravity = "Down",
                    RandomSeed = 17,
                    Entities =
                    [
                        new ScenarioEntity
                        {
                            Alias = "player",
                            Position = new ScenarioPosition { X = 1, Y = 1 },
                            Interactions = ["BlocksMovement"],
                            PlayerBody = new ScenarioPlayerBody
                            {
                                MoveSpeed = 2.5,
                                PushSpeedMultiplier = 1.5,
                                FacingDirection = "Right",
                            },
                        },
                    ],
                },
                Steps =
                [
                    new ScenarioStep
                    {
                        AdvanceMilliseconds = 100,
                        ExpectEvents = [],
                    },
                ],
                ExpectFinal = new ExpectedFinalState
                {
                    Entities =
                    [
                        new ExpectedEntityState
                        {
                            Alias = "player",
                            Position = new ScenarioPosition { X = 1, Y = 1 },
                            FacingDirection = "Right",
                        },
                    ],
                },
            };
        }

        internal static ScenarioDocument WithName(ScenarioDocument scenario, string name)
        {
            return new ScenarioDocument
            {
                FormatVersion = scenario.FormatVersion,
                Name = name,
                World = scenario.World,
                Steps = scenario.Steps,
                ExpectFinal = scenario.ExpectFinal,
            };
        }

        internal static ScenarioDocument WithWorld(
            ScenarioDocument scenario,
            int? width = null,
            int? height = null,
            string? gravity = null,
            List<ScenarioEntity>? entities = null)
        {
            ScenarioWorld world = scenario.World;
            return new ScenarioDocument
            {
                FormatVersion = scenario.FormatVersion,
                Name = scenario.Name,
                World = new ScenarioWorld
                {
                    Width = width ?? world.Width,
                    Height = height ?? world.Height,
                    Gravity = gravity ?? world.Gravity,
                    RandomSeed = world.RandomSeed,
                    Entities = entities ?? world.Entities,
                },
                Steps = scenario.Steps,
                ExpectFinal = scenario.ExpectFinal,
            };
        }

        internal static ScenarioDocument WithSteps(
            ScenarioDocument scenario,
            List<ScenarioStep> steps)
        {
            return new ScenarioDocument
            {
                FormatVersion = scenario.FormatVersion,
                Name = scenario.Name,
                World = scenario.World,
                Steps = steps,
                ExpectFinal = scenario.ExpectFinal,
            };
        }

        internal static ScenarioDocument WithFinal(
            ScenarioDocument scenario,
            List<ExpectedEntityState> entities)
        {
            return new ScenarioDocument
            {
                FormatVersion = scenario.FormatVersion,
                Name = scenario.Name,
                World = scenario.World,
                Steps = scenario.Steps,
                ExpectFinal = new ExpectedFinalState { Entities = entities },
            };
        }
    }
}
