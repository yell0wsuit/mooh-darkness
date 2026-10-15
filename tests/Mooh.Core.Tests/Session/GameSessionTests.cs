using System;
using System.Collections.Generic;
using System.Text.Json;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Player;
using Mooh.Core.Session;
using Mooh.Core.Simulation;

using Xunit;

namespace Mooh.Core.Tests.Session
{
    /// <summary>
    /// Verifies recovered progression across a pack. The original has no lives: a death ends the
    /// run and the level is replayed on request, while a win re-enters through <c>GameInit</c>,
    /// which restores health, weapons and bonuses but never rolls back the cumulative score.
    /// </summary>
    public sealed class GameSessionTests
    {
        /// <summary>A win already settling enters the door even if lethal damage occurs during that settle.</summary>
        [Fact]
        public void WinningOverridesALethalReactionDuringTheSettle()
        {
            GameSession session = CreateSession();
            _ = session.Advance(TimeSpan.FromSeconds(3));
            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromMilliseconds(100));
            Entity player = session.Level.World.Player!;
            _ = session.Simulation.ApplyDamage(new DamageRequest(player.Id, player.Id, DamageKind.Fire, 1000));
            Assert.Equal(PlayerReactionKind.Dead, player.PlayerBody!.Reaction.Kind);

            _ = session.Advance(TimeSpan.FromMilliseconds(1900));

            Assert.Equal(GameSessionState.LevelCompleted, session.State);
            Assert.Equal(PlayerActionKind.LevelExit, player.PlayerBody.Action!.Kind);
            Assert.Equal(PlayerReactionKind.Normal, player.PlayerBody.Reaction.Kind);
            Assert.Equal(TimeSpan.Zero, player.PlayerBody.Reaction.Remaining);
        }

        /// <summary>A long advance consumes the completion walk after the win settle within that same advance.</summary>
        [Fact]
        public void ALongAdvanceDoesNotDelayTheCompletionWalk()
        {
            GameSession session = CreateSession();
            _ = session.Advance(TimeSpan.FromSeconds(3));
            RemoveMonster(session);

            _ = session.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(PlayerActionKind.LevelExited, session.Level.World.Player!.PlayerBody!.Action!.Kind);
        }

        /// <summary>Winning begins the door exit instead of leaving the player controllable.</summary>
        [Fact]
        public void WinningBeginsTheDoorExit()
        {
            GameSession session = CreateSession();
            _ = session.Advance(TimeSpan.FromSeconds(3));
            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(GameSessionState.LevelCompleted, session.State);
            Assert.Equal("LevelExit", session.Level.World.Player!.PlayerBody!.Action?.Kind.ToString());
            Assert.Contains(session.Level.World.Entities, entity => entity.DoorBody is not null);
        }

        /// <summary>The player disappears after the entry walk and the door leaves after its closing animation.</summary>
        [Fact]
        public void TheExitDoorClosesAndLeavesWhileThePlayerStaysHidden()
        {
            GameSession session = CreateSession();
            _ = session.Advance(TimeSpan.FromSeconds(3));
            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(2));
            Entity door = Assert.Single(session.Level.World.Entities, entity => entity.DoorBody is not null);

            _ = session.AdvanceOutcome(TimeSpan.FromMilliseconds(874));
            Assert.Equal(PlayerActionKind.LevelExit, session.Level.World.Player!.PlayerBody!.Action!.Kind);
            Assert.Null(door.DoorBody!.RemovalRemainingSeconds);
            _ = session.AdvanceOutcome(TimeSpan.FromMilliseconds(1));
            Assert.Equal(PlayerActionKind.LevelExited, session.Level.World.Player!.PlayerBody!.Action!.Kind);
            Assert.Equal(0.9, door.DoorBody.RemovalRemainingSeconds!.Value, 10);
            _ = session.AdvanceOutcome(TimeSpan.FromMilliseconds(899));
            Assert.Contains(door, session.Level.World.Entities);
            _ = session.AdvanceOutcome(TimeSpan.FromMilliseconds(1));
            Assert.DoesNotContain(door, session.Level.World.Entities);
            Assert.Equal(PlayerActionKind.LevelExited, session.Level.World.Player!.PlayerBody!.Action!.Kind);
        }

        /// <summary>Both the completion walk and the hidden player are immune without delaying completion.</summary>
        [Fact]
        public void TheCompletionPlayerIsImmuneAndDoesNotHoldTheOutcomeOpen()
        {
            GameSession session = CreateSession();
            _ = session.Advance(TimeSpan.FromSeconds(3));
            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(2));
            Entity player = session.Level.World.Player!;
            double health = player.HealthBody!.Current;

            Assert.True(player.PlayerBody!.IsImmortal);
            Assert.False(player.PlayerBody.HoldsLevelEndOpen);
            _ = session.Simulation.ApplyDamage(new DamageRequest(player.Id, player.Id, DamageKind.Fire, 1000));
            Assert.Equal(health, player.HealthBody.Current);
            _ = session.AdvanceOutcome(TimeSpan.FromSeconds(1));
            Assert.True(player.PlayerBody.IsImmortal);
            Assert.False(player.PlayerBody.HoldsLevelEndOpen);
            Assert.Equal(health, player.HealthBody.Current);
        }

        /// <summary>A final campaign level enters the same completion door as every other level.</summary>
        [Fact]
        public void CompletingTheCampaignAlsoEntersTheDoor()
        {
            GameSession session = CreateSession(startingLevelIndex: 1);
            _ = session.Advance(TimeSpan.FromSeconds(3));
            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(GameSessionState.CampaignCompleted, session.State);
            Assert.Equal(PlayerActionKind.LevelExit, session.Level.World.Player!.PlayerBody!.Action!.Kind);
            _ = session.AdvanceOutcome(TimeSpan.FromSeconds(1));
            Assert.Equal(PlayerActionKind.LevelExited, session.Level.World.Player.PlayerBody.Action!.Kind);
        }

        /// <summary>Verifies a new session starts on the first level with no banked score.</summary>
        [Fact]
        public void NewSessionStartsOnTheFirstLevel()
        {
            GameSession session = CreateSession();

            Assert.Equal(0, session.LevelIndex);
            Assert.Equal(2, session.LevelCount);
            Assert.Equal(GameSessionState.Playing, session.State);
            Assert.Equal(0, session.TotalScore);
        }

        /// <summary>Verifies destroying the last lock carrier completes a non-final level.</summary>
        [Fact]
        public void WinningANonFinalLevelCompletesIt()
        {
            GameSession session = CreateSession();

            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(GameSessionState.LevelCompleted, session.State);
            Assert.Equal(0, session.LevelIndex);
        }

        /// <summary>
        /// Verifies advancing re-enters through the recovered per-level reset: full health and the
        /// default weapon loadout on a freshly materialized level.
        /// </summary>
        [Fact]
        public void AdvancingLoadsTheNextLevelAndAppliesTheRecoveredReset()
        {
            GameSession session = CreateSession();
            Entity player = session.Level.World.Player!;
            _ = session.Simulation.ApplyDamage(new DamageRequest(
                player.Id,
                player.Id,
                DamageKind.Explosion,
                40));
            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(3));

            session.AdvanceToNextLevel();

            Assert.Equal(1, session.LevelIndex);
            Assert.Equal(GameSessionState.Playing, session.State);
            PlayerBody body = session.Level.World.Player!.PlayerBody!;
            Assert.Equal(100, session.Level.World.Player!.HealthBody!.Current);
            Assert.Equal(5, body.Progress.Grenades);
            Assert.Equal(PlayerReactionKind.Normal, body.Reaction.Kind);
        }

        /// <summary>Verifies winning the last level in the pack completes the campaign.</summary>
        [Fact]
        public void WinningTheFinalLevelCompletesTheCampaign()
        {
            GameSession session = CreateSession(startingLevelIndex: 1);

            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(GameSessionState.CampaignCompleted, session.State);
            Assert.Equal(1, session.LevelIndex);
        }

        /// <summary>Verifies fatal damage fails the level rather than consuming a life.</summary>
        [Fact]
        public void FatalDamageFailsTheLevel()
        {
            GameSession session = CreateSession();
            Emerge(session);
            Entity player = session.Level.World.Player!;

            _ = session.Simulation.ApplyDamage(new DamageRequest(
                player.Id,
                player.Id,
                DamageKind.Explosion,
                100));
            _ = session.Advance(TimeSpan.FromSeconds(2));

            Assert.Equal(GameSessionState.LevelFailed, session.State);
            Assert.Equal(0, session.LevelIndex);
        }

        /// <summary>Verifies restarting reloads the same level exactly as the recovered restart does.</summary>
        [Fact]
        public void RestartingReloadsTheSameLevel()
        {
            GameSession session = CreateSession();
            Entity player = session.Level.World.Player!;
            _ = session.Simulation.ApplyDamage(new DamageRequest(
                player.Id,
                player.Id,
                DamageKind.Explosion,
                100));
            _ = session.Advance(TimeSpan.FromSeconds(2));

            session.RestartLevel();

            Assert.Equal(GameSessionState.Playing, session.State);
            Assert.Equal(0, session.LevelIndex);
            Assert.Equal(100, session.Level.World.Player!.HealthBody!.Current);
            Assert.NotSame(player, session.Level.World.Player);
        }

        /// <summary>
        /// Verifies the cumulative score is monotonic. <c>HScoreScoreAdd</c> only ever adds and
        /// <c>HScoreLvlNew</c> does not subtract, so points earned before a restart are kept.
        /// </summary>
        [Fact]
        public void CumulativeScoreSurvivesARestart()
        {
            GameSession session = CreateSession();
            Emerge(session);
            session.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = session.Advance(TimeSpan.FromMilliseconds(500));

            Assert.Equal(10, session.LevelScore);
            Assert.Equal(10, session.TotalScore);

            session.RestartLevel();

            Assert.Equal(0, session.LevelScore);
            Assert.Equal(10, session.TotalScore);
        }

        /// <summary>Verifies the cumulative score carries across a level boundary.</summary>
        [Fact]
        public void CumulativeScoreCarriesToTheNextLevel()
        {
            GameSession session = CreateSession();
            Emerge(session);
            session.SetPlayerInput(new PlayerInputState(false, false, true, false));
            _ = session.Advance(TimeSpan.FromMilliseconds(500));
            session.SetPlayerInput(default);
            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(3));

            session.AdvanceToNextLevel();

            Assert.Equal(0, session.LevelScore);
            Assert.Equal(10, session.TotalScore);
        }

        /// <summary>Verifies a decided level rejects further advancement.</summary>
        [Fact]
        public void AdvanceThrowsOnceTheLevelIsDecided()
        {
            GameSession session = CreateSession();

            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(3));

            _ = Assert.Throws<InvalidOperationException>(
                () => session.Advance(TimeSpan.FromSeconds(1)));
        }

        /// <summary>Verifies the next level can only be entered from a completed level.</summary>
        [Fact]
        public void AdvanceToNextLevelThrowsUnlessTheLevelIsCompleted()
        {
            GameSession session = CreateSession();

            _ = Assert.Throws<InvalidOperationException>(session.AdvanceToNextLevel);
        }

        /// <summary>Verifies a completed campaign cannot be advanced past its last level.</summary>
        [Fact]
        public void CompletedCampaignCannotBeAdvanced()
        {
            GameSession session = CreateSession(startingLevelIndex: 1);

            RemoveMonster(session);
            _ = session.Advance(TimeSpan.FromSeconds(3));

            _ = Assert.Throws<InvalidOperationException>(session.AdvanceToNextLevel);
        }

        /// <summary>
        /// Plays out the recovered level entry, during which the player emerges from the start door
        /// immortal and uncontrollable.
        /// </summary>
        private static void Emerge(GameSession session)
        {
            _ = session.Advance(TimeSpan.FromMilliseconds(1875));
        }

        private static void RemoveMonster(GameSession session)
        {
            Assert.True(session.Level.World.Remove(session.Level.GetEntity(MonsterObjectId).Id));
        }

        private const int MonsterObjectId = 3;

        private static GameSession CreateSession(int startingLevelIndex = 0)
        {
            LoadedLevelPack pack = new(
                new LevelPackManifest(
                    1,
                    null,
                    [new LevelManifestEntry(0, "one", "one.json"), new LevelManifestEntry(1, "two", "two.json")]),
                [CreateLevel(), CreateLevel()]);
            return new GameSession(pack, DifficultySpeeds.Standard, startingLevelIndex);
        }

        private static LevelDocument CreateLevel()
        {
            // A second coin the player never reaches keeps the recovered 5,000 point
            // all-treasure bonus from firing and masking the per-level score.
            List<LevelObject> objects =
            [
                Object(1, "OPlayer", 1, 1),
                Object(2, "OCoin03", 2, 1),
                Object(MonsterObjectId, "MBlueR", 4, 1),
                Object(4, "OCoin01", 0, 1),
                Object(5, "CellsSolid01", 0, 2),
                Object(6, "CellsSolid01", 1, 2),
                Object(7, "CellsSolid01", 2, 2),
                Object(8, "CellsSolid01", 4, 2),
            ];
            return new LevelDocument(
                1,
                null,
                new LevelSize(6, 4),
                new EditorPreview(0, 0, 6, 4),
                objects);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            Dictionary<string, JsonElement> properties = [];
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], properties);
        }
    }
}
