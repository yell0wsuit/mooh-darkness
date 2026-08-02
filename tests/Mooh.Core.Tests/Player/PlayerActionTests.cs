using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Player
{
    /// <summary>
    /// Verifies the recovered <c>GamePlayer.DoCtrl</c> fire branch: a weapon is placed when the
    /// faced cell can hold one, the hand strike runs otherwise, and both share one repeat
    /// timestamp while suppressing walking entirely.
    /// </summary>
    public sealed class PlayerActionTests
    {
        /// <summary>Verifies a grenade is placed in an empty faced cell after the recovered delay.</summary>
        [Fact]
        public void FiringIntoAnEmptyCellPlacesAGrenade()
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "CellsMetal01", 1, 3));
            Entity player = level.World.Player!;
            level.Simulation.SetPlayerInput(Fire());

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(350));

            Assert.Contains(events, value => value is PlayerGrenadeThrowStarted);
            PlayerGrenadePlaced placed = Assert.Single(events.OfType<PlayerGrenadePlaced>());
            Assert.Equal(new GridPosition(1, 2), placed.Position);
            Assert.Equal(4, player.PlayerBody!.Progress.Grenades);
            Entity grenade = level.World.GetEntity(placed.GrenadeEntityId);
            Assert.Equal(EntityKind.Grenade, grenade.ArchivalIdentity!.Kind);
            Assert.Equal(new GridPosition(1, 2), grenade.Position);
        }

        /// <summary>
        /// Verifies the grenade appears only when the recovered placing animation ends, not when
        /// the control is pressed.
        /// </summary>
        [Fact]
        public void TheGrenadeAppearsOnlyWhenThePlacingAnimationEnds()
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "CellsMetal01", 1, 3));
            level.Simulation.SetPlayerInput(Fire());

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(300));

            Assert.DoesNotContain(events, value => value is PlayerGrenadePlaced);
            Assert.Equal(
                PlayerActionKind.GrenadeThrow,
                level.World.Player!.PlayerBody!.Action!.Kind);
            Assert.Equal(5, level.World.Player.PlayerBody.Progress.Grenades);
        }

        /// <summary>
        /// Verifies diggable terrain does not block a placed weapon: it carries the recovered
        /// crushable bit, so <c>Game._CellPlKill</c> destroys it and the grenade takes the cell.
        /// </summary>
        [Fact]
        public void FiringIntoDiggableTerrainClearsItAndPlacesTheGrenade()
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "CellsEat01", 1, 2),
                Object(3, "CellsMetal01", 1, 3));
            EntityId terrainId = level.GetEntity(2).Id;
            level.Simulation.SetPlayerInput(Fire());

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(350));

            Assert.DoesNotContain(level.World.Entities, entity => entity.Id == terrainId);
            Assert.Contains(
                events,
                value => value is EntityDestroyed destroyed && destroyed.EntityId == terrainId);
            _ = Assert.Single(events.OfType<PlayerGrenadePlaced>());
        }

        /// <summary>Verifies a blocked faced cell routes the fire control to the hand strike.</summary>
        [Fact]
        public void FiringIntoABlockedCellStrikesByHand()
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "CellsSolid01", 1, 2));
            level.Simulation.SetPlayerInput(Fire());

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(10));

            PlayerStrikeStarted started = Assert.Single(events.OfType<PlayerStrikeStarted>());
            Assert.Equal(Direction.Down, started.Direction);
            Assert.Equal(TimeSpan.FromMilliseconds(105), started.Duration);
            Assert.DoesNotContain(events, value => value is PlayerGrenadeThrowStarted);
        }

        /// <summary>
        /// Verifies the recovered per-direction windup durations, which come from the frame counts
        /// of the four <c>ST_StrIn*</c> states at 15 ms per frame.
        /// </summary>
        /// <param name="facing">The direction struck.</param>
        /// <param name="milliseconds">The recovered windup duration.</param>
        [Theory]
        [InlineData(Direction.Left, 150)]
        [InlineData(Direction.Up, 135)]
        [InlineData(Direction.Right, 150)]
        [InlineData(Direction.Down, 105)]
        public void TheStrikeWindupUsesTheRecoveredPerDirectionDuration(
            Direction facing,
            int milliseconds)
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "CellsSolid01", 0, 1),
                Object(3, "CellsSolid01", 1, 0),
                Object(4, "CellsSolid01", 2, 1),
                Object(5, "CellsSolid01", 1, 2));

            // The player starts facing down, so every other direction is reached by holding it
            // against a blocked cell, which turns the player without moving it.
            level.Simulation.SetPlayerInput(Hold(facing));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(400));
            level.Simulation.SetPlayerInput(Fire());

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(1));

            Assert.Equal(new GridPosition(1, 1), level.World.Player!.Position);
            Assert.Equal(
                TimeSpan.FromMilliseconds(milliseconds),
                Assert.Single(events.OfType<PlayerStrikeStarted>()).Duration);
        }

        /// <summary>
        /// Verifies the strike consumes the faced cell, which is how a standing player collects
        /// and digs without walking. A collectible carries no crushable bit, so it blocks a placed
        /// weapon and the fire control reaches the strike branch instead.
        /// </summary>
        [Fact]
        public void TheStrikeConsumesTheFacedCell()
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "OCoin03", 1, 2),
                Object(3, "CellsMetal01", 1, 3),
                Object(4, "CellsMetal01", 0, 3),
                Object(5, "CellsMetal01", 2, 3));
            Entity player = level.World.Player!;
            EntityId coinId = level.GetEntity(2).Id;
            level.Simulation.SetPlayerInput(Fire());

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(110));

            Assert.Contains(events, value => value is PlayerStruck);
            Assert.Contains(
                events,
                value => value is PlayerAte ate && ate.EatenEntityId == coinId);
            Assert.DoesNotContain(level.World.Entities, entity => entity.Id == coinId);
            Assert.Equal(new GridPosition(1, 1), player.Position);

            // The coin is worth 10 and is the level's only treasure, so collecting it also awards
            // the recovered all-treasure bonus.
            Assert.Equal(5010, player.PlayerBody!.Progress.Score);
        }

        /// <summary>
        /// Verifies the strike is uncontrollable: the recovered strike states carry no control
        /// map, so held directions are ignored until the recovery ends.
        /// </summary>
        [Fact]
        public void TheStrikeIgnoresHeldDirectionsUntilItEnds()
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "CellsSolid01", 1, 2));
            Entity player = level.World.Player!;
            level.Simulation.SetPlayerInput(Fire());
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(10));

            level.Simulation.SetPlayerInput(Hold(Direction.Left));
            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(120));

            Assert.NotNull(player.PlayerBody!.Action);
            Assert.Equal(new GridPosition(1, 1), player.Position);
            Assert.Equal(Direction.Down, player.PlayerBody.FacingDirection);

            _ = level.Simulation.Advance(TimeSpan.FromMilliseconds(500));

            Assert.Null(player.PlayerBody.Action);
            Assert.Equal(Direction.Left, player.PlayerBody.FacingDirection);
            Assert.Equal(new GridPosition(0, 1), player.Position);
        }

        /// <summary>
        /// Verifies the recovered repeat time. The strike branch repeats no faster than
        /// <c>CNT_StrRTime</c>, and the held control neither walks nor strikes meanwhile.
        /// </summary>
        [Fact]
        public void TheStrikeRepeatsNoFasterThanTheRecoveredRepeatTime()
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "CellsSolid01", 1, 2));
            level.Simulation.SetPlayerInput(Fire());

            IReadOnlyList<SimulationEvent> first = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(900));
            IReadOnlyList<SimulationEvent> second = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(200));

            _ = Assert.Single(first.OfType<PlayerStrikeStarted>());
            _ = Assert.Single(second.OfType<PlayerStrikeStarted>());
        }

        /// <summary>
        /// Verifies the fire control suppresses walking while it is held, matching the recovered
        /// branch that returns before <c>CtrlMove</c> is ever reached.
        /// </summary>
        [Fact]
        public void HoldingFireSuppressesWalkingFromAStandstill()
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "CellsSolid01", 1, 2));
            Entity player = level.World.Player!;
            level.Simulation.SetPlayerInput(new PlayerInputState(
                Left: true,
                Up: false,
                Right: false,
                Down: false,
                Fire: true));

            _ = level.Simulation.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new GridPosition(1, 1), player.Position);
            Assert.Equal(Direction.Down, player.PlayerBody!.FacingDirection);
        }

        /// <summary>
        /// Verifies the recovered no-ammunition path. The five starting grenades are spent one
        /// repeat time apart, and the sixth attempt reports an empty weapon rather than striking.
        /// </summary>
        [Fact]
        public void FiringWithoutGrenadesReportsAnEmptyWeapon()
        {
            MaterializedLevel level = Materialize(
                Object(1, "OPlayer", 1, 1),
                Object(2, "CellsMetal01", 1, 4),
                Object(3, "CellsMetal01", 0, 4),
                Object(4, "CellsMetal01", 2, 4));
            Entity player = level.World.Player!;
            level.Simulation.SetPlayerInput(Fire());

            IReadOnlyList<SimulationEvent> events = level.Simulation.Advance(
                TimeSpan.FromMilliseconds(8000));

            Assert.Equal(5, events.OfType<PlayerGrenadePlaced>().Count());
            Assert.Equal(0, player.PlayerBody!.Progress.Grenades);
            _ = Assert.Single(events.OfType<PlayerWeaponEmpty>());
            Assert.DoesNotContain(events, value => value is PlayerStrikeStarted);
        }

        private static PlayerInputState Fire()
        {
            return new PlayerInputState(
                Left: false,
                Up: false,
                Right: false,
                Down: false,
                Fire: true);
        }

        private static PlayerInputState Hold(Direction direction)
        {
            return new PlayerInputState(
                direction == Direction.Left,
                direction == Direction.Up,
                direction == Direction.Right,
                direction == Direction.Down);
        }

        private static MaterializedLevel Materialize(params LevelObject[] objects)
        {
            return LevelMaterializer.Materialize(
                new LevelDocument(
                    1,
                    null,
                    new LevelSize(8, 6),
                    new EditorPreview(0, 0, 8, 6),
                    objects));
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            Dictionary<string, System.Text.Json.JsonElement> properties = [];
            return new LevelObject(
                id,
                type,
                0,
                new LevelPosition(x, y),
                [],
                properties);
        }
    }
}
