using System;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks sounds attached to gameplay interactions rather than animation states.</summary>
    public sealed class InteractionSoundTests
    {
        /// <summary>Interaction events retain their sound and position after the source is removed.</summary>
        [Theory]
        [InlineData("magic", "MagicWork")]
        [InlineData("ice", "IceWork")]
        [InlineData("beetle", "SmallKill")]
        [InlineData("burn", "FirePop")]
        [InlineData("mutual", "FireBob")]
        [InlineData("consume", "FireBrk")]
        public void RemovedObjectsStillCueTheirInteraction(string interaction, string alias)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            EntityId id = EntityId.New();
            GridPosition cell = new(4, 1);
            SimulationEvent item = interaction switch
            {
                "magic" => new MagicActivated(id, cell),
                "ice" => new FrozenMonsterShattered(id, cell),
                "beetle" => new SmallBeetleKilled(id, cell),
                "burn" => new FireContactResolved(id, cell, FireContactKind.Player),
                "mutual" => new FireContactResolved(id, cell, FireContactKind.MutualDestruction),
                "consume" => new FireContactResolved(id, cell, FireContactKind.Consumed),
                _ => throw new InvalidOperationException(),
            };
            harness.Presenter.Update(harness.Level.World, [], 0);
            harness.Presenter.Update(harness.Level.World, [item], 0);

            Audio.SoundCue cue = Assert.Single(harness.Presenter.Sounds, sound => sound.Alias == alias);
            Assert.Equal((4.0, 1.0), (cue.X, cue.Y));
        }

        /// <summary>Only walking pushes use a default sound when the object's move alias is absent.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnregisteredMoveSoundUsesTheWalkingDefaultOnly(bool strike)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 4, 1)));
            Entity bomb = harness.Level.GetEntity(200);
            harness.Presenter.Update(harness.Level.World, [], 0);
            harness.Presenter.Update(harness.Level.World, [new PushStarted(bomb.Id, Direction.Right, IsStrike: strike)], 0);
            Assert.Equal(!strike, System.Linq.Enumerable.Any(harness.Presenter.Sounds, sound => sound.Alias == "DefaultSlide"));
        }

        /// <summary>Tunnel turnaround cues its own sound in addition to the travelling animation.</summary>
        [Fact]
        public void TunnelTurnaroundPlaysItsFlipSound()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Presenter.Update(harness.Level.World, [], 0);
            harness.Presenter.Update(harness.Level.World,
                [new PlayerTunnelReversed(harness.Player.Id, new GridPosition(3, 1), Direction.Left)], 0);
            _ = Assert.Single(harness.Presenter.Sounds, sound => sound.Alias == "TonFlip");
        }

        /// <summary>Rocket rotation sounds occur at launch and every cell arrival, including impact.</summary>
        [Fact]
        public void RocketRotationSoundsFollowItsCellArrivals()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            EntityId rocket = EntityId.New();
            harness.Presenter.Update(harness.Level.World, [], 0);
            harness.Presenter.Update(harness.Level.World,
                [new RocketFired(harness.Player.Id, rocket, new GridPosition(2, 1), Direction.Right),
                new CellEntered(rocket, new GridPosition(3, 1)),
                new EntityDestroyed(rocket, rocket)], 0);
            Assert.Equal(2, System.Linq.Enumerable.Count(harness.Presenter.Sounds, sound => sound.Alias == "RocketRot"));
            harness.Presenter.Update(harness.Level.World, [new CellEntered(rocket, new GridPosition(4, 1))], 0);
            Assert.DoesNotContain(harness.Presenter.Sounds, sound => sound.Alias == "RocketRot");
        }

        /// <summary>Finishing a real cola drink plays its fizz as protection starts.</summary>
        [Fact]
        public void FinishingColaPlaysItsFizz()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("BCola", 2, 1)));
            harness.Input(new Core.Player.PlayerInputState(false, false, true, false));
            harness.StepUntil(() => harness.Player.PlayerBody!.Progress.ColaRemaining > TimeSpan.Zero, limitSeconds: 4);
            Assert.Contains(harness.Presenter.Sounds, sound => sound.Alias == "ColaOn");
            Assert.Contains(harness.Presenter.Sounds, sound => sound.Alias == "ColaFizz");
        }
        /// <summary>Teleport travel emits recurring pulses while it moves between linked cells.</summary>
        [Fact]
        public void TeleportFlightPlaysItsTravelPulses()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Level.World.Add(new Entity(EntityId.New(), harness.Player.Position,
                Core.Interactions.InteractionMask.OccupancyTransparent,
                teleporterBody: new Core.Hazards.TeleporterBody([new GridPosition(6, 1)])));
            harness.Input(new Core.Player.PlayerInputState(false, false, false, false, Fire: true));
            harness.StepUntil(() => harness.Player.PlayerBody!.Action?.Kind == Core.Player.PlayerActionKind.TeleportFlight, limitSeconds: 2);
            int pulses = System.Linq.Enumerable.Count(harness.Presenter.Sounds, sound => sound.Alias == "TPortMove");
            for (int frame = 0; frame < 60; frame++)
            {
                harness.Step(0.016);
                pulses += System.Linq.Enumerable.Count(harness.Presenter.Sounds, sound => sound.Alias == "TPortMove");
            }

            Assert.InRange(pulses, 3, 5);
        }

        /// <summary>Sheltering schedules three pairs of eating sounds during the healing wait.</summary>
        [Fact]
        public void HealingShelterPlaysItsEatingSounds()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            harness.Begin();
            harness.Step(1.880);
            harness.Input(new Core.Player.PlayerInputState(false, false, false, false, Fire: true));
            harness.Step(0.010);
            harness.Input(default);
            harness.StepUntil(() => harness.Player.PlayerBody!.Action?.Kind == Core.Player.PlayerActionKind.ShelterHidden, limitSeconds: 2);
            int bites = 0;
            int chews = 0;
            for (int frame = 0; frame < 181; frame++)
            {
                harness.Step(0.016);
                bites += System.Linq.Enumerable.Count(harness.Presenter.Sounds, sound => sound.Alias == "EatEat");
                chews += System.Linq.Enumerable.Count(harness.Presenter.Sounds, sound => sound.Alias == "EatEat2");
            }

            Assert.Equal(3, bites);
            Assert.Equal(3, chews);
        }
        /// <summary>Each outward barrel arm has its own sound, while the center uses the main blast.</summary>
        [Fact]
        public void BarrelArmsPlayTheirCellSounds()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("OBarel", 4, 1)));
            Entity barrel = harness.Level.GetEntity(200);
            harness.Step(0);
            harness.Detonate(barrel);
            harness.Step(0);
            _ = Assert.Single(harness.Presenter.Sounds, cue => cue.Alias == "BarrelMainExpl");
            Assert.DoesNotContain(harness.Presenter.Sounds, cue => cue.Alias == "BarrelExpl");
            harness.Step(0.151);
            Assert.Contains(harness.Presenter.Sounds, cue => cue.Alias == "BarrelExpl");
        }

        /// <summary>A placed big bomb sounds at every cell reached by its spreading blast.</summary>
        [Fact]
        public void BigBombFloodPlaysItsCellSounds()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1));
            Entity bomb = new(EntityId.New(), new GridPosition(4, 1), Core.Interactions.InteractionMask.BlocksMovement,
                explosionBody: new Core.Explosions.ExplosionBody(Core.Explosions.ExplosionReaction.BigBomb, false),
                bigBombBody: new Core.Explosions.BigBombBody());
            harness.Level.World.Add(bomb);
            harness.Step(0);
            harness.Detonate(bomb);
            harness.Step(0);
            _ = Assert.Single(harness.Presenter.Sounds, cue => cue.Alias == "BBombExpl");
            harness.Step(0.201);
            int cells = System.Linq.Enumerable.Count(harness.LastEvents, item => item is ExplosionCellReached);
            Assert.True(cells > 0);
            Assert.Equal(cells, System.Linq.Enumerable.Count(harness.Presenter.Sounds, cue => cue.Alias == "BBombExpl"));
        }
        /// <summary>Absorption and ice shattering replace the ordinary monster kill sound.</summary>
        [Theory]
        [InlineData("MGrR", true)]
        [InlineData("MBlueR", true)]
        [InlineData("MGrR", false)]
        [InlineData("MBlueR", false)]
        public void SpecialMonsterDeathsDoNotPlayTheNormalKillSound(string type, bool ice)
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, (type, 4, 1)));
            Entity monster = harness.Level.GetEntity(200);
            harness.Presenter.Update(harness.Level.World, [], 0);
            SimulationEvent special = ice
                ? new FrozenMonsterShattered(monster.Id, monster.Position)
                : new PlayerAte(harness.Player.Id, monster.Id);
            harness.Presenter.Update(harness.Level.World, [new EntityDestroyed(monster.Id, harness.Player.Id), special], 0);
            Assert.DoesNotContain(harness.Presenter.Sounds, cue => cue.Alias is "GreenKill" or "BlueKill");
            _ = Assert.Single(harness.Presenter.Sounds, cue => cue.Alias == (ice ? "IceWork" : "SmallGet"));
        }
    }
}
