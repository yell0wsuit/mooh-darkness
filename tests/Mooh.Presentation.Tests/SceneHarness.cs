using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Scene;

namespace Mooh.Presentation.Tests
{
    /// <summary>Runs a small simulated level under a scene presenter.</summary>
    internal sealed class SceneHarness
    {
        private static readonly Lazy<AnimationLibrary> SharedLibrary = new(() =>
        {
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "animations", "animations.json"));
            return AnimationLibrary.Load(stream);
        });

        private List<SimulationEvent> _pending = [];

        public SceneHarness(LevelDocument document)
        {
            Level = LevelMaterializer.Materialize(document);
            Player = Level.GetEntity(1);
            Presenter = new ScenePresenter(Library, new Random(1));
        }

        /// <summary>Gets the recovered animation tables every test shares.</summary>
        public static AnimationLibrary Library => SharedLibrary.Value;

        public MaterializedLevel Level
        {
            get;
        }

        public Entity Player
        {
            get;
        }

        public ScenePresenter Presenter
        {
            get;
        }

        /// <summary>
        /// Builds an 8×4 level: the player at column 1, a metal floor on row 2, and the extra
        /// objects, numbered from 200.
        /// </summary>
        public static LevelDocument BuildLevel(int playerY, params (string Type, int X, int Y)[] extra)
        {
            return BuildLevel(playerY, [.. extra.Select(item => (item.Type, item.X, item.Y, 0))]);
        }

        /// <summary>Builds the same level with an editor index for each extra object.</summary>
        public static LevelDocument BuildLevel(int playerY, (string Type, int X, int Y, int EditorIndex)[] extra)
        {
            List<LevelObject> objects = [Object(1, "OPlayer", 1, playerY)];
            for (int x = 0; x <= 7; x++)
            {
                objects.Add(Object(100 + x, "CellsMetal01", x, 2));
            }

            int id = 200;
            foreach ((string type, int x, int y, int editorIndex) in extra)
            {
                objects.Add(Object(id++, type, x, y, editorIndex));
            }

            return new LevelDocument(1, null, new LevelSize(8, 4), new EditorPreview(0, 0, 8, 4), objects);
        }

        public EntityAnimator Animator(Entity entity)
        {
            return Presenter.AnimatorOf(entity.Id)
                ?? throw new InvalidOperationException($"No animator for {entity.Id}.");
        }

        public void Input(PlayerInputState input)
        {
            Level.Simulation.SetPlayerInput(input);
        }

        public void Begin()
        {
            _pending.AddRange(Level.Simulation.BeginLevel());
        }

        public void Damage(int amount)
        {
            _pending.AddRange(Level.Simulation.ApplyDamage(new DamageRequest(Player.Id, Player.Id, DamageKind.Fire, amount)));
        }

        public void Step(double seconds)
        {
            _pending.AddRange(Level.Simulation.Advance(TimeSpan.FromSeconds(seconds)));
            Presenter.Update(Level.World, _pending, seconds);
            _pending = [];
        }

        /// <summary>Advances in small steps until a condition holds.</summary>
        public void StepUntil(Func<bool> condition, double limitSeconds = 5, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string what = "")
        {
            for (double elapsed = 0; !condition(); elapsed += 0.016)
            {
                if (elapsed > limitSeconds)
                {
                    throw new InvalidOperationException($"{what} never held; the player is at {Player.Position} doing {Player.PlayerBody?.Action?.Kind}.");
                }

                Step(0.016);
            }
        }

        private static LevelObject Object(int id, string type, int x, int y, int editorIndex = 0)
        {
            return new LevelObject(id, type, editorIndex, new LevelPosition(x, y), [], new Dictionary<string, System.Text.Json.JsonElement>());
        }
    }
}
