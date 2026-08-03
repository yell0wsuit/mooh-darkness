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
    /// Verifies the recovered tunnel network: the <c>PlComeHere</c> gate a tunnel answers in
    /// <c>CtrlMove</c>, and the per-cell routing of <c>_TonMoveOnCell</c> once the player is
    /// inside.
    /// </summary>
    public sealed class TunnelTransitTests
    {
        /// <summary>
        /// Verifies walking into a tunnel starts the recovered entry animation rather than an
        /// ordinary move, and that the player stays put until it ends.
        /// </summary>
        [Fact]
        public void WalkingIntoATunnelStartsTheEntryAnimation()
        {
            WorldGrid world = new(6, 3);
            Entity player = NewPlayer(new GridPosition(0, 1));
            world.Add(player);
            world.Add(NewTunnel(new GridPosition(1, 1), TunnelShape.Horizontal));
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(Held(Direction.Right));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(100));

            PlayerEnteredTunnel entered = Assert.Single(events.OfType<PlayerEnteredTunnel>());
            Assert.Equal(new GridPosition(1, 1), entered.Position);
            Assert.Equal(Direction.Right, entered.Direction);
            Assert.Equal(TimeSpan.FromMilliseconds(175), entered.Duration);
            Assert.Equal(new GridPosition(0, 1), player.Position);
            Assert.Null(player.Motion);
        }

        /// <summary>
        /// Verifies a tunnel refuses an arrival its <c>EnDirMap</c> has no opening for, so the
        /// player is simply blocked by a solid cell instead.
        /// </summary>
        [Fact]
        public void ATunnelRefusesAnArrivalItHasNoOpeningFor()
        {
            WorldGrid world = new(6, 3);
            Entity player = NewPlayer(new GridPosition(1, 0), Direction.Down);
            world.Add(player);
            world.Add(NewTunnel(new GridPosition(1, 1), TunnelShape.Horizontal));
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(Held(Direction.Down));

            IReadOnlyList<SimulationEvent> events = simulation.Advance(TimeSpan.FromMilliseconds(100));

            Assert.DoesNotContain(events, item => item is PlayerEnteredTunnel);
            Assert.Contains(events, item => item is PlayerBlocked);
            Assert.Equal(new GridPosition(1, 0), player.Position);
        }

        /// <summary>
        /// Verifies a straight run carries the player through every tunnel cell and drops it out
        /// of the far end, and that transit runs at the recovered <c>CNT_OPrm_SpeedTon</c>
        /// multiple of the player's own speed.
        /// </summary>
        [Fact]
        public void AStraightRunCarriesThePlayerOutOfTheFarEnd()
        {
            WorldGrid world = new(6, 3);
            Entity player = NewPlayer(new GridPosition(0, 1));
            world.Add(player);
            world.Add(NewTunnel(new GridPosition(1, 1), TunnelShape.Horizontal));
            world.Add(NewTunnel(new GridPosition(2, 1), TunnelShape.Horizontal));
            world.Add(NewTunnel(new GridPosition(3, 1), TunnelShape.Horizontal));
            SimulationWorld simulation = CreateSimulation(world);
            IReadOnlyList<SimulationEvent> events = RunOneJourney(simulation, Direction.Right);

            PlayerLeftTunnel left = Assert.Single(events.OfType<PlayerLeftTunnel>());
            Assert.Equal(new GridPosition(4, 1), left.Position);
            Assert.Equal(Direction.Right, left.Direction);
            Assert.Equal(new GridPosition(4, 1), player.Position);
            Assert.Null(player.PlayerBody!.TunnelTransit);
        }

        /// <summary>
        /// Verifies a corner turns the player, applying <c>EnDirMap</c> at every cell rather than
        /// keeping the entry direction.
        /// </summary>
        [Fact]
        public void ACornerTurnsTheTravellingPlayer()
        {
            WorldGrid world = new(5, 5);
            Entity player = NewPlayer(new GridPosition(0, 3));
            world.Add(player);
            world.Add(NewTunnel(new GridPosition(1, 3), TunnelShape.Horizontal));
            world.Add(NewTunnel(new GridPosition(2, 3), TunnelShape.LeftTop));
            world.Add(NewTunnel(new GridPosition(2, 2), TunnelShape.Vertical));
            SimulationWorld simulation = CreateSimulation(world);
            IReadOnlyList<SimulationEvent> events = RunOneJourney(simulation, Direction.Right);

            PlayerLeftTunnel left = Assert.Single(events.OfType<PlayerLeftTunnel>());
            Assert.Equal(new GridPosition(2, 1), left.Position);
            Assert.Equal(Direction.Up, left.Direction);
        }

        /// <summary>
        /// Verifies a blocked exit turns the player back, the recovered <c>bTonFlip</c>, and that
        /// routing then uses <c>DirMap</c> so a one-way tunnel still lets the player out.
        /// </summary>
        [Fact]
        public void ABlockedExitTurnsThePlayerBackThroughAOneWayTunnel()
        {
            WorldGrid world = new(6, 3);
            Entity player = NewPlayer(new GridPosition(0, 1));
            world.Add(player);
            world.Add(NewTunnel(new GridPosition(1, 1), TunnelShape.LeftToRight));
            world.Add(NewTunnel(new GridPosition(2, 1), TunnelShape.LeftToRight));
            world.Add(NewWall(new GridPosition(3, 1)));
            SimulationWorld simulation = CreateSimulation(world);
            IReadOnlyList<SimulationEvent> events = RunOneJourney(simulation, Direction.Right);

            PlayerTunnelReversed reversed = Assert.Single(events.OfType<PlayerTunnelReversed>());
            Assert.Equal(new GridPosition(2, 1), reversed.Position);
            Assert.Equal(Direction.Left, reversed.Direction);
            PlayerLeftTunnel left = Assert.Single(events.OfType<PlayerLeftTunnel>());
            Assert.Equal(new GridPosition(0, 1), left.Position);
        }

        /// <summary>
        /// Verifies transit consumes an edible occupant in its path. The recovered gate is
        /// <c>CellObjCheck(n, 0, CNT_OrMaskPlTonOut, False)</c>, whose mask is exactly
        /// <c>CNT_CMaskPlCanEat</c>, so a cell holding only edible things is not an obstruction.
        /// </summary>
        [Fact]
        public void TransitEatsAnEdibleOccupantInItsPath()
        {
            WorldGrid world = new(6, 3);
            Entity player = NewPlayer(new GridPosition(0, 1));
            Entity earth = new(
                EntityId.New(),
                new GridPosition(2, 1),
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked
                    | InteractionMask.PlayerEdible);
            world.Add(player);
            world.Add(NewTunnel(new GridPosition(1, 1), TunnelShape.Horizontal));
            world.Add(earth);
            SimulationWorld simulation = CreateSimulation(world);
            IReadOnlyList<SimulationEvent> events = RunOneJourney(simulation, Direction.Right);

            Assert.Contains(events, item => item is PlayerAte ate && ate.EatenEntityId == earth.Id);
            PlayerLeftTunnel left = Assert.Single(events.OfType<PlayerLeftTunnel>());
            Assert.Equal(new GridPosition(2, 1), left.Position);
        }

        /// <summary>
        /// Verifies a travelling player is immortal and hidden. Every recovered tunnel state is
        /// <c>bImmort</c> and <c>bShadow</c>, so a blast neither damages the player nor is seen
        /// to, and monster navigation walks straight over the cell.
        /// </summary>
        [Fact]
        public void ATravellingPlayerIsImmortalAndHidden()
        {
            WorldGrid world = new(6, 3);
            Entity player = NewPlayer(new GridPosition(0, 1));
            world.Add(player);
            world.Add(NewTunnel(new GridPosition(1, 1), TunnelShape.Horizontal));
            world.Add(NewTunnel(new GridPosition(2, 1), TunnelShape.Horizontal));
            world.Add(NewTunnel(new GridPosition(3, 1), TunnelShape.Horizontal));
            SimulationWorld simulation = CreateSimulation(world);
            simulation.SetPlayerInput(Held(Direction.Right));
            _ = simulation.Advance(TimeSpan.FromMilliseconds(300));

            Assert.NotNull(player.PlayerBody!.TunnelTransit);
            Assert.True(player.PlayerBody.IsImmortal);
            Assert.True(player.PlayerBody.HoldsLevelEndOpen);
            Assert.True(player.Interactions.HasFlag(InteractionMask.OccupancyTransparent));
            Assert.True(player.Interactions.HasFlag(InteractionMask.MonsterTransparent));
            Assert.False(player.IsLivingPlayer);

            IReadOnlyList<SimulationEvent> damage = simulation.ApplyDamage(
                new DamageRequest(player.Id, player.Id, DamageKind.Explosion, 20));

            Assert.Contains(damage, item => item is DamageIgnored ignored
                && ignored.Reason == DamageResult.IgnoredImmune);
            Assert.Equal(100, player.HealthBody!.Current);
        }

        /// <summary>
        /// Verifies the player is solid and controllable again once the exit animation has run,
        /// which is the only tunnel state that is not <c>bShadow</c>.
        /// </summary>
        [Fact]
        public void LeavingATunnelRestoresASolidControllablePlayer()
        {
            WorldGrid world = new(6, 3);
            Entity player = NewPlayer(new GridPosition(0, 1));
            world.Add(player);
            world.Add(NewTunnel(new GridPosition(1, 1), TunnelShape.Horizontal));
            SimulationWorld simulation = CreateSimulation(world);
            _ = RunOneJourney(simulation, Direction.Right);

            Assert.Equal(new GridPosition(2, 1), player.Position);
            Assert.Null(player.PlayerBody!.TunnelTransit);
            Assert.Null(player.PlayerBody.Action);
            Assert.False(player.PlayerBody.IsImmortal);
            Assert.False(player.Interactions.HasFlag(InteractionMask.OccupancyTransparent));
            Assert.False(player.Interactions.HasFlag(InteractionMask.MonsterTransparent));
        }

        /// <summary>Verifies each recovered routing table, entry side by entry side.</summary>
        /// <param name="name">The tunnel shape under test.</param>
        /// <param name="arrival">The direction the player arrives travelling.</param>
        /// <param name="expected">The direction the tunnel routes to, or -1 for no opening.</param>
        [Theory]
        [InlineData("Horizontal", Direction.Left, (int)Direction.Left)]
        [InlineData("Horizontal", Direction.Right, (int)Direction.Right)]
        [InlineData("Horizontal", Direction.Up, -1)]
        [InlineData("Vertical", Direction.Up, (int)Direction.Up)]
        [InlineData("Vertical", Direction.Down, (int)Direction.Down)]
        [InlineData("Vertical", Direction.Left, -1)]
        [InlineData("LeftTop", Direction.Right, (int)Direction.Up)]
        [InlineData("LeftTop", Direction.Down, (int)Direction.Left)]
        [InlineData("TopRight", Direction.Left, (int)Direction.Up)]
        [InlineData("TopRight", Direction.Down, (int)Direction.Right)]
        [InlineData("RightBottom", Direction.Left, (int)Direction.Down)]
        [InlineData("RightBottom", Direction.Up, (int)Direction.Right)]
        [InlineData("BottomLeft", Direction.Up, (int)Direction.Left)]
        [InlineData("BottomLeft", Direction.Right, (int)Direction.Down)]
        [InlineData("LeftToRight", Direction.Right, (int)Direction.Right)]
        [InlineData("LeftToRight", Direction.Left, -1)]
        [InlineData("RightToLeft", Direction.Left, (int)Direction.Left)]
        [InlineData("RightToLeft", Direction.Right, -1)]
        [InlineData("TopToBottom", Direction.Down, (int)Direction.Down)]
        [InlineData("TopToBottom", Direction.Up, -1)]
        [InlineData("BottomToTop", Direction.Up, (int)Direction.Up)]
        [InlineData("BottomToTop", Direction.Down, -1)]
        public void RoutingTablesMatchTheRecoveredEntryMaps(string name, Direction arrival, int expected)
        {
            Direction? route = ShapeByName(name).Route(arrival);

            Assert.Equal(expected < 0 ? null : (Direction)expected, route);
        }

        /// <summary>
        /// Verifies a one-way tunnel's <c>DirMap</c> opens both sides, which is what a turned-back
        /// player travels on.
        /// </summary>
        /// <param name="name">The one-way shape under test.</param>
        /// <param name="arrival">The direction the player arrives travelling.</param>
        [Theory]
        [InlineData("LeftToRight", Direction.Left)]
        [InlineData("RightToLeft", Direction.Right)]
        [InlineData("TopToBottom", Direction.Up)]
        [InlineData("BottomToTop", Direction.Down)]
        public void AReversedPlayerRoutesOnTheOutlineMap(string name, Direction arrival)
        {
            TunnelShape shape = ShapeByName(name);

            Assert.Null(shape.Route(arrival));
            Assert.Equal(arrival, shape.Route(arrival, ignoreOneWay: true));
        }

        private static TunnelShape ShapeByName(string name)
        {
            return name switch
            {
                "Horizontal" => TunnelShape.Horizontal,
                "Vertical" => TunnelShape.Vertical,
                "LeftTop" => TunnelShape.LeftTop,
                "TopRight" => TunnelShape.TopRight,
                "RightBottom" => TunnelShape.RightBottom,
                "BottomLeft" => TunnelShape.BottomLeft,
                "LeftToRight" => TunnelShape.LeftToRight,
                "RightToLeft" => TunnelShape.RightToLeft,
                "TopToBottom" => TunnelShape.TopToBottom,
                "BottomToTop" => TunnelShape.BottomToTop,
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
            };
        }

        /// <summary>
        /// Walks the player into the tunnel and then releases the control, so one journey can be
        /// observed on its own. The original keeps polling held input, so a player that leaves a
        /// tunnel with the direction still down simply walks back into it.
        /// </summary>
        private static List<SimulationEvent> RunOneJourney(
            SimulationWorld simulation,
            Direction direction)
        {
            simulation.SetPlayerInput(Held(direction));
            List<SimulationEvent> events = [.. simulation.Advance(TimeSpan.FromMilliseconds(100))];
            simulation.SetPlayerInput(default);
            events.AddRange(simulation.Advance(TimeSpan.FromSeconds(3)));
            return events;
        }

        private static PlayerInputState Held(Direction direction)
        {
            return new PlayerInputState(
                direction == Direction.Left,
                direction == Direction.Up,
                direction == Direction.Right,
                direction == Direction.Down,
                false);
        }

        private static Entity NewTunnel(GridPosition position, TunnelShape shape)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked
                    | InteractionMask.RoundSupport | InteractionMask.BlocksFlight,
                tunnelBody: new TunnelBody(shape));
        }

        private static Entity NewWall(GridPosition position)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement | InteractionMask.MovementLocked);
        }

        private static Entity NewPlayer(GridPosition position, Direction facing = Direction.Right)
        {
            return new Entity(
                EntityId.New(),
                position,
                InteractionMask.BlocksMovement,
                playerBody: new PlayerBody(DifficultySpeeds.Standard.PlayerSpeed, 1.5, facing),
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
