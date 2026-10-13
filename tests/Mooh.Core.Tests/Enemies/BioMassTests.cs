using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Enemies
{
    /// <summary>
    /// Verifies the recovered bio mass, <c>CGameObjBioMass</c>. Each time its 20-frame
    /// <c>ST_Stay</c> loop ends, <c>Rand.Prob(CNT_BioSpawnProb)</c> may start a 500 ms spawn; at its
    /// end a random open or bio-strikable neighbor is chosen, where the bio mass either shakes
    /// every bio-strike target or, finding none, sends a new bio mass sliding in at four times the
    /// normal speed.
    /// </summary>
    public sealed class BioMassTests
    {
        /// <summary>
        /// Verifies a bio mass spreads into its one open neighbor. With every draw at its lowest the
        /// loop runs at 45 ms a frame, 900 ms, and the spawn takes 500 ms more.
        /// </summary>
        [Fact]
        public void ABioMassSpreadsIntoAnOpenNeighbor()
        {
            (SimulationWorld simulation, WorldGrid world) = Create(ScriptedRandom.Lowest);
            Entity bio = AddBio(world, 2, 1);
            WallIn(world, 2, 1, open: Direction.Right);

            IReadOnlyList<SimulationEvent> before = simulation.Advance(TimeSpan.FromMilliseconds(1399));
            IReadOnlyList<SimulationEvent> at = simulation.Advance(TimeSpan.FromMilliseconds(2));

            Assert.DoesNotContain(before, item => item is BioMassSpread);
            BioMassSpread spread = Assert.Single(at.OfType<BioMassSpread>());
            Assert.Equal(bio.Id, spread.EntityId);
            Assert.Equal(new GridPosition(3, 1), spread.To);
            Entity child = world.GetEntity(spread.ChildEntityId);
            Assert.Equal(new GridPosition(3, 1), child.Position);
            Assert.Equal(
                DifficultySpeeds.Standard.NormalSpeed * 4,
                child.Motion!.Speed,
                9);
        }

        /// <summary>Verifies a bio mass never spreads while the recovered probability keeps missing.</summary>
        [Fact]
        public void ABioMassIdlesWhileTheProbabilityMisses()
        {
            (SimulationWorld simulation, WorldGrid world) = Create(ScriptedRandom.Highest);
            _ = AddBio(world, 2, 1);
            WallIn(world, 2, 1, open: Direction.Right);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(10));

            Assert.DoesNotContain(events, item => item is BioMassSpread);
        }

        /// <summary>
        /// Verifies a bio mass strikes the player in its chosen neighbor instead of spreading, and the
        /// player's <c>OnShake</c> is shake damage.
        /// </summary>
        [Fact]
        public void ABioMassStrikesThePlayerInsteadOfSpreading()
        {
            (SimulationWorld simulation, WorldGrid world) = Create(ScriptedRandom.Lowest, withPlayerAt: new GridPosition(3, 1));
            Entity bio = AddBio(world, 2, 1);
            WallIn(world, 2, 1, open: Direction.Right);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(1500));

            Assert.Contains(events, item => item is DamageApplied { Kind: DamageKind.Shake } damage
                && damage.SourceEntityId == bio.Id);
            Assert.DoesNotContain(events, item => item is BioMassSpread);
        }

        /// <summary>Verifies a shake sets a bio mass off in its own 3x3 blast.</summary>
        [Fact]
        public void AShakeSetsABioMassOff()
        {
            (SimulationWorld simulation, WorldGrid world) = Create(ScriptedRandom.Highest);
            Entity bio = AddBio(world, 2, 3);
            Entity rock = LevelMaterializer.CreateRuntimeObject(
                EntityId.New(),
                new GridPosition(2, 0),
                "ORock01",
                DifficultySpeeds.Standard);
            world.Add(rock);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(2));

            Assert.Contains(events, item => item == new ExplosionStarted(bio.Id, new GridPosition(2, 3)));
        }

        private static Entity AddBio(WorldGrid world, int x, int y)
        {
            Entity bio = LevelMaterializer.CreateRuntimeObject(
                EntityId.New(),
                new GridPosition(x, y),
                "MMBio",
                DifficultySpeeds.Standard);
            world.Add(bio);
            return bio;
        }

        /// <summary>Surrounds a cell with metal on every side but one.</summary>
        private static void WallIn(WorldGrid world, int x, int y, Direction open)
        {
            foreach (Direction side in (Direction[])[Direction.Left, Direction.Up, Direction.Right, Direction.Down])
            {
                GridPosition cell = new GridPosition(x, y).Offset(side);
                if (side != open && world.Contains(cell))
                {
                    world.Add(LevelMaterializer.CreateRuntimeObject(
                        EntityId.New(),
                        cell,
                        "CellsMetal01",
                        DifficultySpeeds.Standard));
                }
            }
        }

        private static (SimulationWorld Simulation, WorldGrid World) Create(
            ScriptedRandom random,
            GridPosition? withPlayerAt = null)
        {
            WorldGrid world = new(6, 4);
            if (withPlayerAt is GridPosition position)
            {
                world.Add(new Entity(
                    EntityId.New(),
                    position,
                    InteractionMask.BlocksMovement,
                    playerBody: new PlayerBody(2.5, 1.5, Direction.Down),
                    healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(650))));
            }

            return (
                new SimulationWorld(
                    world,
                    Direction.Down,
                    new DefaultGravityCollisionPolicy(),
                    random,
                    0.5,
                    Undrained.Standard),
                world);
        }

        /// <summary>
        /// A random source pinned to one end of every range, so each recovered random choice is
        /// known in advance.
        /// </summary>
        internal sealed class ScriptedRandom(bool highest) : Random
        {
            /// <summary>Gets a source that always draws the lowest value.</summary>
            public static ScriptedRandom Lowest => new(highest: false);

            /// <summary>Gets a source that always draws the highest value.</summary>
            public static ScriptedRandom Highest => new(highest: true);

            /// <inheritdoc/>
            public override int Next(int maxValue)
            {
                return highest ? maxValue - 1 : 0;
            }

            /// <inheritdoc/>
            public override int Next(int minValue, int maxValue)
            {
                return highest ? maxValue - 1 : minValue;
            }

            /// <inheritdoc/>
            public override double NextDouble()
            {
                return highest ? 0.999999 : 0;
            }
        }
    }
}
