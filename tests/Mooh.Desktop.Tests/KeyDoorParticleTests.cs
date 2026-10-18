using System;
using System.IO;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Scene;
using Mooh.Presentation.Sprites;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Checks door-unlock sparks against the actual final-frame atlas pixels.</summary>
    public sealed class KeyDoorParticleTests
    {
        /// <summary>Sparks sample the final opened frame on the source's seven-pixel grid.</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void UnlockSamplesTheActualFinalDoorPixels(int key)
        {
            string assets = Path.Combine(AppContext.BaseDirectory, "assets");
            using FileStream stream = File.OpenRead(Path.Combine(assets, "animations", "animations.json"));
            AnimationLibrary library = AnimationLibrary.Load(stream);
            using SpriteMasks masks = new(Path.Combine(assets, "sprites"));
            ScenePresenter presenter = new(library, new Random(1), masks: masks);
            WorldGrid world = new(5, 4);
            Entity player = new(EntityId.New(), new GridPosition(1, 1), InteractionMask.None,
                playerBody: new PlayerBody(2.5, 1.5, Direction.Right));
            Entity door = LevelMaterializer.CreateRuntimeObject(EntityId.New(), new GridPosition(2, 1), $"OKDDoor{key}", DifficultySpeeds.Standard);
            world.Add(player);
            world.Add(door);
            presenter.Update(world, [], 0);
            Assert.True(world.Remove(door.Id));
            presenter.Update(world, [new KeyDoorOpened(player.Id, door.Id, key, 100, door.Position)], 0);

            SpriteFrame frame = Assert.IsType<SpriteFrame>(masks.FrameOf("SpriteObjKeyDoor", $"DSt{key}_14"));
            int expected = 0;
            for (int y = 0; y < frame.Height; y += 7)
            {
                for (int x = 0; x < frame.Width; x += 7)
                {
                    if (masks.IsSolid("SpriteObjKeyDoor", frame.X + x, frame.Y + y))
                    {
                        expected++;
                    }
                }
            }

            Assert.True(expected > 0);
            Assert.Equal(expected, presenter.Sprites.Count(sprite => sprite.Frame.StartsWith("PGetStar", StringComparison.Ordinal)));
        }
    }
}
