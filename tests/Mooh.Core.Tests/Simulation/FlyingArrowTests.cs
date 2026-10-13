using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Gravity;
using Mooh.Core.Grid;
using Mooh.Core.Hazards;
using Mooh.Core.Interactions;
using Mooh.Core.Player;
using Mooh.Core.Shakes;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Simulation
{
    /// <summary>
    /// Verifies the recovered <c>_DoFly_&lt;dir&gt;</c> the four <c>OArrow*</c> types name for
    /// <c>OnLand</c>, <c>OnShake</c>, <c>OnShakeHard</c>, <c>OnExpl</c> and <c>PlCtrl</c> alike.
    /// These are the flying arrows; the gravity-flipping <c>CGameObjAntiGArrow</c> is never
    /// placed in the committed corpus.
    /// </summary>
    public sealed class FlyingArrowTests
    {
        /// <summary>
        /// Verifies an arrow that lands launches itself, taking the recovered 245 ms
        /// <c>ST_Work</c> windup before it starts flying.
        /// </summary>
        [Fact]
        public void ALandingArrowLaunchesItself()
        {
            WorldGrid world = new(8, 4);
            Entity arrow = NewArrow(new GridPosition(1, 0), Direction.Right);
            world.Add(arrow);
            world.Add(NewMetal(new GridPosition(1, 2)));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(1));

            ArrowLaunched launched = Assert.Single(events.OfType<ArrowLaunched>());
            Assert.Equal(Direction.Right, launched.Direction);
            Assert.Equal(TimeSpan.FromMilliseconds(245), launched.Duration);
            Assert.True(arrow.FlyingArrowBody!.IsLaunched);
            Assert.Null(arrow.GravityBody);
        }

        /// <summary>
        /// Verifies an arrow flies in its own fixed direction whatever triggered it, so a
        /// downward-falling arrow that points right leaves along the row it landed in.
        /// </summary>
        [Fact]
        public void AnArrowFliesInItsOwnDirectionNotTheTriggerDirection()
        {
            WorldGrid world = new(8, 4);
            Entity arrow = NewArrow(new GridPosition(1, 0), Direction.Right);
            world.Add(arrow);
            world.Add(NewMetal(new GridPosition(1, 2)));
            SimulationWorld simulation = CreateSimulation(world);

            _ = simulation.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(1, arrow.Position.Y);
            Assert.True(arrow.Position.X > 1);
        }

        /// <summary>
        /// Verifies a flying arrow bores through terrain carrying the recovered
        /// <c>CNT_CMaskFlyThrought</c> bit instead of detonating against it.
        /// </summary>
        [Fact]
        public void AFlyingArrowBoresThroughBreakableTerrain()
        {
            WorldGrid world = new(8, 5);
            Entity arrow = NewArrow(new GridPosition(1, 3), Direction.Right, hasGravity: false);
            Entity earth = NewBreakableTerrain(new GridPosition(3, 3));
            world.Add(arrow);
            world.Add(earth);
            world.Add(NewDroppedWeight(new GridPosition(1, 1)));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(3));

            Assert.Contains(events, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == earth.Id);
            Assert.DoesNotContain(world.Entities, entity => entity.Id == earth.Id);
        }

        /// <summary>
        /// Verifies immortal metal terrain stops an arrow dead. Metal is the one terrain family
        /// without <c>CNT_CMaskFlyThrought</c>, so the arrow detonates its own 3x3 blast against
        /// it instead of passing through.
        /// </summary>
        [Fact]
        public void MetalTerrainDetonatesAnArrowInstead()
        {
            WorldGrid world = new(8, 5);
            Entity arrow = NewArrow(new GridPosition(1, 3), Direction.Right, hasGravity: false);
            Entity metal = NewMetal(new GridPosition(3, 3));
            world.Add(arrow);
            world.Add(metal);
            world.Add(NewDroppedWeight(new GridPosition(1, 1)));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(3));

            // The recovered blast is ObjExplRange3x3(self), so it is centred on the arrow's own
            // cell rather than on the obstruction it stopped against.
            Assert.Contains(events, item => item is ExplosionStarted started
                && started.EntityId == arrow.Id
                && started.Position == new GridPosition(2, 3));
            Assert.Contains(world.Entities, entity => entity.Id == metal.Id);
            _ = Assert.Throws<KeyNotFoundException>(() => world.GetEntity(arrow.Id));
        }

        /// <summary>
        /// Verifies an arrow that stops against the player shakes the player's cell, and the
        /// player's recovered <c>OnShake</c> is shake damage, landing at once rather than when the
        /// arrow's own blast reaches the cell 200 ms later.
        /// </summary>
        [Fact]
        public void AnArrowStoppingAgainstThePlayerShakesItAtOnce()
        {
            WorldGrid world = new(8, 5);
            Entity arrow = NewArrow(new GridPosition(1, 3), Direction.Right, hasGravity: false);
            Entity player = NewPlayer(new GridPosition(3, 3));
            world.Add(arrow);
            world.Add(player);
            world.Add(NewDroppedWeight(new GridPosition(1, 1)));
            SimulationWorld simulation = CreateSimulation(world);

            List<SimulationEvent> events = [.. simulation.Advance(TimeSpan.FromSeconds(3))];

            int started = events.FindIndex(item => item is ExplosionStarted { EntityId: var id } && id == arrow.Id);
            int shaken = events.FindIndex(item => item is DamageApplied { Kind: DamageKind.Shake } damage
                && damage.EntityId == player.Id
                && damage.SourceEntityId == arrow.Id);
            Assert.InRange(shaken, started + 1, started + 4);
        }

        /// <summary>
        /// Verifies a blast launches an arrow rather than destroying it, which is the recovered
        /// <c>OnExpl</c> the four types share with every other trigger.
        /// </summary>
        [Fact]
        public void ABlastLaunchesAnArrowRatherThanDestroyingIt()
        {
            WorldGrid world = new(8, 4);
            Entity arrow = NewArrow(new GridPosition(2, 1), Direction.Right);
            Entity bomb = new(
                EntityId.New(),
                new GridPosition(1, 1),
                InteractionMask.BlocksMovement,
                explosionBody: new ExplosionBody(ExplosionReaction.Detonate3x3, false));
            world.Add(arrow);
            world.Add(bomb);
            SimulationWorld simulation = CreateSimulation(world);

            List<SimulationEvent> all = [.. simulation.Detonate(bomb.Id)];
            all.AddRange(simulation.Advance(TimeSpan.FromMilliseconds(300)));

            Assert.Contains(all, item => item is ArrowLaunched launched
                && launched.EntityId == arrow.Id);
            Assert.DoesNotContain(all, item => item is EntityDestroyed destroyed
                && destroyed.EntityId == arrow.Id);
        }

        /// <summary>
        /// Verifies a landing shake launches an arrow, the recovered <c>OnShake</c>.
        /// </summary>
        [Fact]
        public void AShakeLaunchesAnArrow()
        {
            WorldGrid world = new(8, 5);
            Entity arrow = NewArrow(new GridPosition(1, 3), Direction.Right, hasGravity: false);
            world.Add(arrow);
            world.Add(new Entity(
                EntityId.New(),
                new GridPosition(1, 0),
                InteractionMask.None,
                new GravityBody(4, canSlide: false)));
            SimulationWorld simulation = CreateSimulation(world);

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromSeconds(2));

            Assert.Contains(events, item => item is ArrowLaunched launched
                && launched.EntityId == arrow.Id);
        }

        /// <summary>
        /// Verifies a hand strike launches an arrow instead of pushing it. The recovered
        /// <c>_AEnd_Str</c> offers <c>PlCtrl</c> before <c>OnPlPush</c> and takes the first that
        /// answers, and the arrow is the only placed type that is pushable and defines both.
        /// </summary>
        [Fact]
        public void AHandStrikeLaunchesAnArrowInsteadOfPushingIt()
        {
            WorldGrid world = new(8, 4);
            Entity player = NewPlayer(new GridPosition(1, 1));
            Entity arrow = NewArrow(new GridPosition(2, 1), Direction.Up, hasGravity: false);
            world.Add(player);
            world.Add(arrow);
            world.Add(NewMetal(new GridPosition(1, 2)));
            world.Add(NewMetal(new GridPosition(2, 2)));
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(new PlayerInputState(false, false, false, false, true));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(400));

            Assert.Contains(events, item => item is ArrowLaunched launched
                && launched.EntityId == arrow.Id);
            Assert.DoesNotContain(events, item => item is PushStarted pushed
                && pushed.EntityId == arrow.Id);
        }

        /// <summary>
        /// Verifies a second trigger while the arrow is already launched changes nothing, because
        /// the original would only re-enter the state the arrow is already in.
        /// </summary>
        [Fact]
        public void ARelaunchIsIgnored()
        {
            WorldGrid world = new(8, 4);
            Entity arrow = NewArrow(new GridPosition(1, 1), Direction.Right, hasGravity: false);
            world.Add(arrow);
            SimulationWorld simulation = CreateSimulation(world);
            List<SimulationEvent> events = [];

            simulation.LaunchArrow(arrow, events);
            simulation.LaunchArrow(arrow, events);

            _ = Assert.Single(events.OfType<ArrowLaunched>());
        }

        private static Entity NewArrow(
            GridPosition position,
            Direction direction,
            bool hasGravity = true)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.PlayerPushable
                    | InteractionMask.RoundSupport | InteractionMask.BlueMonsterTarget,
                hasGravity ? new GravityBody(2, canSlide: true) : null,
                explosionBody: new ExplosionBody(ExplosionReaction.LaunchArrow, false),
                shakeBody: new ShakeBody(ShakeReaction.LaunchArrow, ShakeReaction.LaunchArrow, reactsToLanding: true),
                flyingArrowBody: new FlyingArrowBody(direction));
        }

        /// <summary>Creates a plain falling body used only to shake the cell it lands on.</summary>
        private static Entity NewDroppedWeight(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.None,
                new GravityBody(4, canSlide: false));
        }

        private static Entity NewBreakableTerrain(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked
                    | InteractionMask.BlocksFlight | InteractionMask.DestroyedByFlyingArrow,
                explosionBody: new ExplosionBody(ExplosionReaction.Destroy, false));
        }

        private static Entity NewMetal(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked
                    | InteractionMask.RoundSupport | InteractionMask.BlocksFlight);
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
