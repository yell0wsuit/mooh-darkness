using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies the recovered <c>CGameObjTPort.PlCtrl</c> and the player's <c>TPortMe</c> flight.
    /// The teleporter is reached through the <c>PlCtrl</c> dispatch <c>DoCtrl</c> runs over the
    /// player's own cell before any weapon, so using one costs no ammunition and no repeat time.
    /// </summary>
    public sealed class TeleporterTests
    {
        /// <summary>
        /// Verifies firing while standing on a linked teleporter starts the recovered
        /// <c>ST_TPortIn</c> and carries the player to the linked cell.
        /// </summary>
        [Fact]
        public void FiringOnATeleporterCarriesThePlayerToItsLink()
        {
            WorldGrid world = new(12, 6);
            Entity player = NewPlayer(new GridPosition(1, 1));
            world.Add(player);
            world.Add(NewTeleporter(new GridPosition(1, 1), [new GridPosition(9, 4)]));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = RunOneUse(simulation);

            PlayerTeleportStarted started = Assert.Single(events.OfType<PlayerTeleportStarted>());
            Assert.Equal(new GridPosition(1, 1), started.Origin);
            Assert.Equal(new GridPosition(9, 4), started.Destination);
            Assert.Equal(TimeSpan.FromSeconds(1), started.Duration);
            PlayerTeleportArrived arrived = Assert.Single(events.OfType<PlayerTeleportArrived>());
            Assert.Equal(new GridPosition(9, 4), arrived.Position);
            Assert.Equal(new GridPosition(9, 4), player.Position);
        }

        /// <summary>
        /// Verifies the recovered flight speed. <c>CNT_TPortSp</c> is 0.00375 cells per
        /// millisecond, so the straight-line distance decides the duration; an eight-cell hop
        /// takes the two one-second animations plus the flight itself.
        /// </summary>
        [Fact]
        public void FlightTakesTheStraightLineDistanceAtTheRecoveredSpeed()
        {
            WorldGrid world = new(12, 6);
            Entity player = NewPlayer(new GridPosition(1, 1));
            world.Add(player);
            world.Add(NewTeleporter(new GridPosition(1, 1), [new GridPosition(9, 1)]));
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(Fire);

            // One second of entry animation, then the eight-cell flight at 3.75 cells a second.
            _ = simulation.Advance(TimeSpan.FromMilliseconds(1000 + 2133));

            Assert.Equal(PlayerActionKind.TeleportFlight, player.PlayerBody!.Action!.Kind);
            Assert.Equal(new GridPosition(1, 1), player.Position);

            _ = simulation.Advance(TimeSpan.FromMilliseconds(2));

            Assert.Equal(new GridPosition(9, 1), player.Position);
        }

        /// <summary>
        /// Verifies successive uses cycle through the links rather than always taking the first,
        /// applying the recovered <c>LinkCoords[1:] + LinkCoords[:1]</c> rotation.
        /// </summary>
        [Fact]
        public void SuccessiveUsesCycleThroughTheLinks()
        {
            TeleporterBody body = new([
                new GridPosition(4, 0),
                new GridPosition(5, 0),
                new GridPosition(6, 0),
            ]);
            GridPosition own = new(1, 1);

            Assert.Equal(new GridPosition(4, 0), body.TakeNextDestination(own));
            Assert.Equal(new GridPosition(5, 0), body.TakeNextDestination(own));
            Assert.Equal(new GridPosition(6, 0), body.TakeNextDestination(own));
            Assert.Equal(new GridPosition(4, 0), body.TakeNextDestination(own));
        }

        /// <summary>
        /// Verifies an unlinked teleporter sends the player to its own cell, which is the
        /// original's way of spelling "go nowhere". Fourteen of the forty-six placed teleporters
        /// are unlinked, so this is the common case, not a degenerate one.
        /// </summary>
        [Fact]
        public void AnUnlinkedTeleporterSendsThePlayerNowhere()
        {
            WorldGrid world = new(12, 6);
            Entity player = NewPlayer(new GridPosition(1, 1));
            world.Add(player);
            world.Add(NewTeleporter(new GridPosition(1, 1), []));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = RunOneUse(simulation);

            PlayerTeleportArrived arrived = Assert.Single(events.OfType<PlayerTeleportArrived>());
            Assert.Equal(new GridPosition(1, 1), arrived.Position);
            Assert.Equal(new GridPosition(1, 1), player.Position);
        }

        /// <summary>
        /// Verifies a destination cell that is still occupied is blown open, the recovered
        /// <c>Game._ObjExplWrd</c> call in <c>_TPortCFunc</c>. Without it a teleporter linked into
        /// solid rock would bury the player.
        /// </summary>
        [Fact]
        public void AnOccupiedDestinationIsBlownOpen()
        {
            WorldGrid world = new(12, 6);
            Entity player = NewPlayer(new GridPosition(1, 1));
            Entity rock = new(
                EntityId.New(),
                new GridPosition(9, 1),
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked,
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, false));
            world.Add(player);
            world.Add(rock);
            world.Add(NewTeleporter(new GridPosition(1, 1), [new GridPosition(9, 1)]));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = RunOneUse(simulation);

            Assert.Contains(events, item => item is ExplosionCellReached reached
                && reached.Position == new GridPosition(9, 1));
            Assert.Contains(events, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == rock.Id);
            Assert.Equal(new GridPosition(9, 1), player.Position);
        }

        /// <summary>
        /// Verifies a teleporting player is immortal and hidden for the flight and solid again by
        /// the time the exit animation has run. Every recovered teleport state is <c>bImmort</c>
        /// and <c>bLockLvlEnd</c>, and only <c>ST_TPortOut</c> drops the shadow.
        /// </summary>
        [Fact]
        public void ATeleportingPlayerIsImmortalAndHiddenUntilItArrives()
        {
            WorldGrid world = new(12, 6);
            Entity player = NewPlayer(new GridPosition(1, 1));
            world.Add(player);
            world.Add(NewTeleporter(new GridPosition(1, 1), [new GridPosition(9, 4)]));
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(Fire);
            _ = simulation.Advance(TimeSpan.FromMilliseconds(500));

            Assert.True(player.PlayerBody!.IsImmortal);
            Assert.True(player.PlayerBody.HoldsLevelEndOpen);
            Assert.True(player.Interactions.HasFlag(InteractionMask.OccupancyTransparent));
            Assert.False(player.IsLivingPlayer);

            IReadOnlyList<SimulationEvent> damage = simulation.ApplyDamage(
                new DamageRequest(player.Id, player.Id, DamageKind.Explosion, 20));

            Assert.Contains(damage, item => item is DamageIgnored ignored
                && ignored.Reason == DamageResult.IgnoredImmune);

            simulation.SetPlayerInput(default);
            _ = simulation.Advance(TimeSpan.FromSeconds(6));

            Assert.Null(player.PlayerBody.Action);
            Assert.Null(player.PlayerBody.Teleport);
            Assert.False(player.PlayerBody.IsImmortal);
            Assert.False(player.Interactions.HasFlag(InteractionMask.OccupancyTransparent));
            Assert.Equal(100, player.HealthBody!.Current);
        }

        /// <summary>
        /// Verifies the teleporter consumes the fire control outright, so no grenade is thrown and
        /// none is spent. The recovered <c>DoCtrl</c> returns from the <c>PlCtrl</c> dispatch
        /// before it reaches the weapon branch or its repeat check.
        /// </summary>
        [Fact]
        public void UsingATeleporterSpendsNoWeapon()
        {
            WorldGrid world = new(12, 6);
            Entity player = NewPlayer(new GridPosition(1, 1));
            world.Add(player);
            world.Add(NewTeleporter(new GridPosition(1, 1), [new GridPosition(9, 4)]));
            SimulationWorld simulation = CreateSimulation(world);
            int grenades = player.PlayerBody!.Progress.Grenades;

            IReadOnlyList<SimulationEvent> events = RunOneUse(simulation);

            Assert.DoesNotContain(events, item => item is PlayerGrenadeThrowStarted);
            Assert.DoesNotContain(events, item => item is PlayerStrikeStarted);
            Assert.Equal(grenades, player.PlayerBody.Progress.Grenades);
            Assert.Null(player.PlayerBody.LastActionSeconds);
        }

        /// <summary>
        /// Verifies the links survive the archival JSON into the runtime world. The recovered
        /// <c>CLinkCoordBase.__getstate__</c> nests the pair list one level deep, so a wrong
        /// reading yields silently unlinked teleporters rather than an error; asserting the whole
        /// corpus is what makes that visible.
        /// </summary>
        [Fact]
        public void PlacedTeleporterLinksSurviveMaterialization()
        {
            string manifestPath = Path.Combine(
                AppContext.BaseDirectory,
                "assets",
                "levels",
                "manifest.json");
            LoadedLevelPack pack = JsonLevelPackLoader.Load(manifestPath);

            List<TeleporterBody> teleporters = [.. pack.Levels
                .Select(LevelMaterializer.Materialize)
                .SelectMany(level => level.World.Entities)
                .Select(entity => entity.TeleporterBody)
                .OfType<TeleporterBody>()];

            Assert.Equal(46, teleporters.Count);
            Assert.Equal(46, teleporters.Sum(teleporter => teleporter.Links.Count));
            Assert.Equal(14, teleporters.Count(teleporter => teleporter.Links.Count == 0));
            Assert.Equal(6, teleporters.Max(teleporter => teleporter.Links.Count));
        }

        /// <summary>
        /// Fires once and then releases the control, so one journey can be observed on its own.
        /// The original keeps polling, so a player still holding fire when it reappears uses the
        /// teleporter again — or, once it has landed somewhere without one, throws a grenade.
        /// </summary>
        private static List<SimulationEvent> RunOneUse(SimulationWorld simulation)
        {
            simulation.SetPlayerInput(Fire);
            List<SimulationEvent> events = [.. simulation.Advance(TimeSpan.FromMilliseconds(100))];
            simulation.SetPlayerInput(default);
            events.AddRange(simulation.Advance(TimeSpan.FromSeconds(5)));
            return events;
        }

        private static PlayerInputState Fire => new(false, false, false, false, true);

        private static Entity NewTeleporter(GridPosition position, GridPosition[] links)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.MonsterTransparent | InteractionMask.OccupancyTransparent,
                teleporterBody: new TeleporterBody(links));
        }

        private static Entity NewPlayer(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(
                    DifficultySpeeds.Standard.PlayerSpeed,
                    1.5,
                    Direction.Right),
                healthBody: new HealthBody(100, 100, TimeSpan.FromMilliseconds(650)));
        }

        private static SimulationWorld CreateSimulation(WorldGrid world)
        {
            return new SimulationWorld(
                world,
                Direction.Down,
                new DefaultGravityCollisionPolicy(),
                new Random(0),
                0.5,
                DifficultySpeeds.Standard);
        }
    }
}
