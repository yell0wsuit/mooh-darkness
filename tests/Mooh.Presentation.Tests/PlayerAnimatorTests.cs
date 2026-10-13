using System;
using System.Collections.Generic;
using System.IO;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the player animator shows the recovered <c>CGameObjPlayer</c> state for what the
    /// simulation is doing, with its ground or flying frames.
    /// </summary>
    public sealed class PlayerAnimatorTests
    {
        private static readonly Lazy<AnimationLibrary> Library = new(() =>
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "animations", "animations.json"));
            return AnimationLibrary.Load(stream);
        });

        /// <summary>Verifies a player standing on the floor shows the single standing frame.</summary>
        [Fact]
        public void AStandingPlayerShowsItsStandingFrame()
        {
            Harness harness = new(Level());

            harness.Step(0.016);

            Assert.Equal("st_stbottom", harness.Animator.Animation.State);
            Assert.Equal("OWlkRotL2T3", harness.Animator.Animation.Frame);
        }

        /// <summary>Verifies a player with nothing below shows the flying frames, <c>_OnCell05_Fly</c>.</summary>
        [Fact]
        public void APlayerWithNothingBelowShowsItsFlyingFrames()
        {
            Harness harness = new(Level(playerY: 0));

            harness.Step(0.016);

            Assert.Equal("st_stbottom", harness.Animator.Animation.State);
            Assert.Equal(10, harness.Animator.Animation.Frames.Count);
        }

        /// <summary>Verifies a held new direction turns the player first, then walks.</summary>
        [Fact]
        public void AHeldDirectionTurnsThenWalks()
        {
            Harness harness = new(Level());
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.Step(0.050);
            Assert.Equal("st_rotb2r", harness.Animator.Animation.State);

            harness.Step(0.100);
            Assert.Equal("st_wlkright", harness.Animator.Animation.State);
        }

        /// <summary>Verifies walking into a pushable rock shows the pushing walk.</summary>
        [Fact]
        public void PushingARockShowsThePushingWalk()
        {
            Harness harness = new(Level(extra: [("ORock01", 2, 1)]));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.Step(0.150);

            Assert.Equal("st_wlkpushright", harness.Animator.Animation.State);
        }

        /// <summary>Verifies pushing against a wall plays the standing push.</summary>
        [Fact]
        public void PushingAWallPlaysTheStandingPush()
        {
            Harness harness = new(Level(extra: [("CellsMetal01", 2, 1)]));
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: true, Down: false));

            harness.Step(0.150);

            Assert.Equal("st_stpushright", harness.Animator.Animation.State);
        }

        /// <summary>Verifies a strike plays its wind-up, then its recovery.</summary>
        [Fact]
        public void AStrikePlaysItsWindupThenItsRecovery()
        {
            Harness harness = new(Level());
            harness.Input(new PlayerInputState(Left: false, Up: false, Right: false, Down: false, Fire: true));

            harness.Step(0.016);
            Assert.Equal("st_strinbottom", harness.Animator.Animation.State);

            harness.Input(default);
            harness.Step(0.100);
            Assert.Equal("st_stroutbottom", harness.Animator.Animation.State);
        }

        /// <summary>Verifies the player waits hidden in the start door, then walks out of it.</summary>
        [Fact]
        public void ThePlayerWaitsHiddenInTheStartDoor()
        {
            Harness harness = new(Level());
            harness.Begin();

            harness.Step(0.016);
            Assert.Equal("st_gstartinstart", harness.Animator.Animation.State);
            Assert.Null(harness.Animator.Animation.Frame);

            harness.Step(1.500);
            Assert.Equal("st_gstartininwalk", harness.Animator.Animation.State);
            Assert.NotNull(harness.Animator.Animation.Frame);
        }

        /// <summary>Verifies a killed player plays the death chain.</summary>
        [Fact]
        public void AKilledPlayerPlaysTheDeathChain()
        {
            Harness harness = new(Level());
            harness.Damage(1000);

            harness.Step(0.016);

            Assert.Equal("st_dead", harness.Animator.Animation.State);
        }

        private static LevelDocument Level(int playerY = 1, (string Type, int X, int Y)[]? extra = null)
        {
            List<LevelObject> objects = [Object(1, "OPlayer", 1, playerY)];
            for (int x = 0; x <= 7; x++)
            {
                objects.Add(Object(100 + x, "CellsMetal01", x, 2));
            }

            int id = 200;
            foreach ((string type, int x, int y) in extra ?? [])
            {
                objects.Add(Object(id++, type, x, y));
            }

            return new LevelDocument(1, null, new LevelSize(8, 4), new EditorPreview(0, 0, 8, 4), objects);
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], new Dictionary<string, System.Text.Json.JsonElement>());
        }

        private sealed class Harness
        {
            private readonly MaterializedLevel _level;
            private readonly Entity _player;
            private List<SimulationEvent> _pending = [];

            public Harness(LevelDocument document)
            {
                _level = LevelMaterializer.Materialize(document);
                _player = _level.GetEntity(1);
                Animator = new PlayerAnimator(Library.Value, new Random(1));
            }

            public PlayerAnimator Animator
            {
                get;
            }

            public void Input(PlayerInputState input)
            {
                _level.Simulation.SetPlayerInput(input);
            }

            public void Begin()
            {
                _pending.AddRange(_level.Simulation.BeginLevel());
            }

            public void Damage(int amount)
            {
                _pending.AddRange(_level.Simulation.ApplyDamage(new DamageRequest(_player.Id, _player.Id, DamageKind.Fire, amount)));
            }

            public void Step(double seconds)
            {
                _pending.AddRange(_level.Simulation.Advance(TimeSpan.FromSeconds(seconds)));
                Animator.Update(_level.World, _player, _pending, seconds);
                _pending = [];
            }
        }
    }
}
