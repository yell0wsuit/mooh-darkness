using System;

using Xunit;

namespace Mooh.SimulationRunner.Tests
{
    /// <summary>Verifies complete in-memory deterministic scenario execution.</summary>
    public sealed class ScenarioExecutorTests
    {
        /// <summary>Verifies a real falling scenario checks events and final motion.</summary>
        [Fact]
        public void RunsSelfVerifyingFallingScenario()
        {
            ScenarioDocument scenario = CreateFallingScenario();

            ScenarioRunResult result = ScenarioExecutor.Run(scenario, "falling.json");

            Assert.True(result.Passed, result.Diagnostic);
            Assert.Equal("falling", result.Name);
            Assert.Null(result.FailureKind);
        }

        /// <summary>Verifies a behavioral mismatch becomes a focused failed result.</summary>
        [Fact]
        public void ReturnsFailedResultForEventMismatch()
        {
            ScenarioDocument scenario = CreateFallingScenario();
            scenario.Steps[0].ExpectEvents.Clear();

            ScenarioRunResult result = ScenarioExecutor.Run(scenario, "mismatch.json");

            Assert.False(result.Passed);
            Assert.Equal(ScenarioFailureKind.Expectation, result.FailureKind);
            Assert.Contains("steps[0].expectEvents", result.Diagnostic, StringComparison.Ordinal);
        }

        /// <summary>Verifies an elapsed duration outside <see cref="TimeSpan"/> is a validation failure.</summary>
        [Fact]
        public void ReturnsValidationFailureForUnrepresentableDuration()
        {
            ScenarioDocument scenario = CreateFallingScenario();
            scenario = ScenarioTestData.WithSteps(
                scenario,
                [
                    new ScenarioStep
                    {
                        AdvanceMilliseconds = long.MaxValue,
                        ExpectEvents = [],
                    },
                ]);

            ScenarioRunResult result = ScenarioExecutor.Run(scenario, "duration.json");

            Assert.False(result.Passed);
            Assert.Equal(ScenarioFailureKind.Validation, result.FailureKind);
            Assert.Contains("advanceMilliseconds", result.Diagnostic, StringComparison.Ordinal);
        }

        /// <summary>Verifies a declared landing explosive is observable end to end.</summary>
        [Fact]
        public void RunsSelfVerifyingLandingExplosionScenario()
        {
            ScenarioDocument scenario = CreateLandingExplosionScenario();

            ScenarioRunResult result = ScenarioExecutor.Run(scenario, "explosion.json");

            Assert.True(result.Passed, result.Diagnostic);
            Assert.Equal("landing explosion", result.Name);
        }

        private static ScenarioDocument CreateFallingScenario()
        {
            return new ScenarioDocument
            {
                FormatVersion = 1,
                Name = "falling",
                World = new ScenarioWorld
                {
                    Width = 3,
                    Height = 3,
                    Gravity = "Down",
                    RandomSeed = 0,
                    Entities =
                    [
                        new ScenarioEntity
                        {
                            Alias = "rock",
                            Position = new ScenarioPosition { X = 1, Y = 0 },
                            Interactions = [],
                            GravityBody = new ScenarioGravityBody
                            {
                                FallSpeed = 1,
                                CanSlide = false,
                            },
                        },
                    ],
                },
                Steps =
                [
                    new ScenarioStep
                    {
                        AdvanceMilliseconds = 1000,
                        ExpectEvents =
                        [
                            new ExpectedEvent
                            {
                                Type = "FallStarted",
                                Entity = "rock",
                                Direction = "Down",
                            },
                            new ExpectedEvent
                            {
                                Type = "CellEntered",
                                Entity = "rock",
                                Position = new ScenarioPosition { X = 1, Y = 1 },
                            },
                        ],
                    },
                ],
                ExpectFinal = new ExpectedFinalState
                {
                    Entities =
                    [
                        new ExpectedEntityState
                        {
                            Alias = "rock",
                            Position = new ScenarioPosition { X = 1, Y = 2 },
                            Motion = new ExpectedMotionState
                            {
                                Cause = "Falling",
                                SegmentDirection = "Down",
                                TravelSign = 1,
                                Speed = 1,
                                Progress = 0,
                                ResolutionOrder = 0,
                            },
                        },
                    ],
                },
            };
        }

        private static ScenarioDocument CreateLandingExplosionScenario()
        {
            return new ScenarioDocument
            {
                FormatVersion = 1,
                Name = "landing explosion",
                World = new ScenarioWorld
                {
                    Width = 3,
                    Height = 4,
                    Gravity = "Down",
                    RandomSeed = 0,
                    Entities =
                    [
                        new ScenarioEntity
                        {
                            Alias = "bomb",
                            Position = new ScenarioPosition { X = 1, Y = 0 },
                            Interactions = [],
                            GravityBody = new ScenarioGravityBody
                            {
                                FallSpeed = 1,
                                CanSlide = false,
                            },
                            ExplosionBody = new ScenarioExplosionBody
                            {
                                Reaction = "Detonate3x3",
                                DetonatesOnLanding = true,
                            },
                        },
                        new ScenarioEntity
                        {
                            Alias = "support",
                            Position = new ScenarioPosition { X = 1, Y = 2 },
                            Interactions = ["BlocksMovement"],
                        },
                    ],
                },
                Steps =
                [
                    new ScenarioStep
                    {
                        AdvanceMilliseconds = 1000,
                        ExpectEvents =
                        [
                            new ExpectedEvent
                            {
                                Type = "FallStarted",
                                Entity = "bomb",
                                Direction = "Down",
                            },
                            new ExpectedEvent
                            {
                                Type = "CellEntered",
                                Entity = "bomb",
                                Position = new ScenarioPosition { X = 1, Y = 1 },
                            },
                            new ExpectedEvent
                            {
                                Type = "Landed",
                                Entity = "bomb",
                                Direction = "Down",
                            },
                            new ExpectedEvent
                            {
                                Type = "ExplosionStarted",
                                Entity = "bomb",
                                Position = new ScenarioPosition { X = 1, Y = 1 },
                            },
                            new ExpectedEvent
                            {
                                Type = "EntityDestroyed",
                                Entity = "bomb",
                                SourceEntity = "bomb",
                            },
                            new ExpectedEvent
                            {
                                Type = "ExplosionCellReached",
                                Entity = "bomb",
                                Position = new ScenarioPosition { X = 1, Y = 1 },
                            },
                        ],
                    },
                ],
                ExpectFinal = new ExpectedFinalState
                {
                    Entities =
                    [
                        new ExpectedEntityState
                        {
                            Alias = "support",
                            Position = new ScenarioPosition { X = 1, Y = 2 },
                        },
                    ],
                },
            };
        }
    }
}
