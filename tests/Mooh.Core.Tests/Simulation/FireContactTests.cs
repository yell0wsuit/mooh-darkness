using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies the recovered <c>_ObjHere_Fire</c>, the handler fire registers through
    /// <c>EvObjHere</c>. Fire has three outcomes and they differ by who arrived: the player is
    /// burned and the fire is spent, a <c>bFireKill</c> object and the fire destroy each other,
    /// and anything else is consumed while the fire survives.
    /// </summary>
    public sealed class FireContactTests
    {
        /// <summary>
        /// Verifies a player walking into fire is damaged for the recovered <c>Compl.HFire</c>
        /// and the fire is spent, which is the recovered order: <c>ObjKill(self)</c> first, then
        /// <c>PObj.OnFire()</c>.
        /// </summary>
        [Fact]
        public void PlayerWalkingIntoFireIsBurnedAndSpendsTheFire()
        {
            WorldGrid world = new(4, 3);
            Entity player = NewPlayer(new GridPosition(1, 1));
            Entity fire = NewFire(new GridPosition(2, 1));
            world.Add(player);
            world.Add(fire);
            world.Add(NewFloor(new GridPosition(1, 2)));
            world.Add(NewFloor(new GridPosition(2, 2)));
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(new PlayerInputState(false, false, true, false, false));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new GridPosition(2, 1), player.Position);
            Assert.Contains(events, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == fire.Id);
            DamageApplied damage = Assert.Single(events.OfType<DamageApplied>());
            Assert.Equal(DamageKind.Fire, damage.Kind);
            Assert.Equal(DifficultySpeeds.Standard.FireDamage, damage.Amount);
            Assert.Equal(100 - DifficultySpeeds.Standard.FireDamage, player.HealthBody!.Current);
        }

        /// <summary>
        /// Verifies difficulty zero uses the lower recovered <c>HFire</c>, which is 30 rather
        /// than the 40 both standard profiles deal.
        /// </summary>
        [Fact]
        public void FireDamageFollowsTheDifficultyProfile()
        {
            Assert.Equal(30, DifficultySpeeds.DifficultyZero.FireDamage);
            Assert.Equal(40, DifficultySpeeds.Standard.FireDamage);
            Assert.Equal(40, DifficultySpeeds.Timed.FireDamage);
        }

        /// <summary>
        /// Verifies an ordinary object falling into fire is consumed while the fire survives,
        /// which is the recovered <c>else</c> branch: only <c>Other</c> is killed.
        /// </summary>
        [Fact]
        public void FallingObjectIsConsumedAndTheFireSurvives()
        {
            WorldGrid world = new(3, 5);
            Entity rock = NewFalling(new GridPosition(1, 0));
            Entity fire = NewFire(new GridPosition(1, 3));
            world.Add(rock);
            world.Add(fire);
            world.Add(NewFloor(new GridPosition(1, 4)));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Contains(events, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == rock.Id);
            Assert.DoesNotContain(events, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == fire.Id);
            Assert.Equal(new GridPosition(1, 3), world.GetEntity(fire.Id).Position);
        }

        /// <summary>
        /// Verifies a <c>bFireKill</c> carrier and the fire destroy each other. The bubble is the
        /// only placed type carrying the flag.
        /// </summary>
        [Fact]
        public void FireKillCarrierAndFireDestroyEachOther()
        {
            WorldGrid world = new(3, 5);
            Entity bubble = NewFalling(
                new GridPosition(1, 0),
                new FireBody(FireContact.MutualDestruction));
            Entity fire = NewFire(new GridPosition(1, 3));
            world.Add(bubble);
            world.Add(fire);
            world.Add(NewFloor(new GridPosition(1, 4)));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Contains(events, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == bubble.Id);
            Assert.Contains(events, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == fire.Id);
        }

        /// <summary>
        /// Verifies fire falling onto a standing player burns nobody. The recovered handler is the
        /// fire's own <c>EvObjHere</c>, so it fires only when another object enters the fire's
        /// cell, never when the fire enters theirs. The fire still lands, and a landing is a
        /// shake like any other, so the player takes the ordinary shake damage and no more.
        /// </summary>
        [Fact]
        public void FireArrivingOnAStandingPlayerBurnsNobody()
        {
            WorldGrid world = new(3, 5);
            Entity player = NewPlayer(new GridPosition(1, 3));
            Entity fire = NewFire(new GridPosition(1, 0));
            world.Add(player);
            world.Add(fire);
            world.Add(NewFloor(new GridPosition(1, 4)));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.DoesNotContain(events, item => item is EntityDestroyed);
            DamageApplied damage = Assert.Single(events.OfType<DamageApplied>());
            Assert.Equal(DamageKind.Shake, damage.Kind);
            Assert.Equal(new GridPosition(1, 2), world.GetEntity(fire.Id).Position);
        }

        /// <summary>
        /// Verifies fire rests on another shadow occupant instead of falling through it. Fire is
        /// the only type setting the recovered <c>bSecondSlideMask</c>, which swaps the support
        /// test to <c>CNT_OrMaskNotEmpty2</c> and so adds the two shadow bits to it.
        /// </summary>
        [Fact]
        public void FireRestsOnAShadowOccupant()
        {
            WorldGrid world = new(3, 5);
            Entity fire = NewFire(new GridPosition(1, 0));
            Entity table = new(
                EntityId.New(),
                new GridPosition(1, 2),
                InteractionMask.MonsterTransparent | InteractionMask.OccupancyTransparent);
            world.Add(fire);
            world.Add(table);
            world.Add(NewFloor(new GridPosition(1, 4)));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(new GridPosition(1, 1), fire.Position);
        }

        /// <summary>
        /// Verifies an ordinary falling body still passes through a shadow occupant, so the
        /// support change belongs to fire alone rather than to every falling object.
        /// </summary>
        [Fact]
        public void OrdinaryFallingBodyPassesThroughAShadowOccupant()
        {
            WorldGrid world = new(3, 5);
            Entity rock = NewFalling(new GridPosition(1, 0));
            world.Add(rock);
            world.Add(new Entity(
                EntityId.New(),
                new GridPosition(1, 2),
                InteractionMask.MonsterTransparent | InteractionMask.OccupancyTransparent));
            world.Add(NewFloor(new GridPosition(1, 4)));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(new GridPosition(1, 3), rock.Position);
        }

        private static Entity NewFire(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.RoundSupport | InteractionMask.OccupancyTransparent,
                new GravityBody(2, canSlide: true, treatsShadowAsSupport: true),
                fireBody: new FireBody(FireContact.Source));
        }

        private static Entity NewFalling(GridPosition position, FireBody? fireBody = null)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                new GravityBody(2, canSlide: false),
                fireBody: fireBody);
        }

        private static Entity NewFloor(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked);
        }

        private static Entity NewPlayer(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(DifficultySpeeds.Standard.PlayerSpeed, 1.5, Direction.Right),
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
