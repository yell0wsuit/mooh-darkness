using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Levels;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Scene;

using SkiaSharp;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Verifies pickup compositing using the real grenade atlas and desktop renderer.</summary>
    public sealed class PickupGlowRenderingTests
    {
        /// <summary>The additive halo lights the surroundings without bleaching opaque grenade pixels.</summary>
        [Fact]
        public void GrenadeGlowPreservesOpaqueBodyPixels()
        {
            List<LevelObject> objects = [Object(1, "OPlayer", 1, 1), Object(2, "WBomb", 5, 1)];
            objects.AddRange(Enumerable.Range(0, 8).Select(x => Object(100 + x, "CellsMetal01", x, 2)));
            MaterializedLevel level = LevelMaterializer.Materialize(new LevelDocument(1, null,
                new LevelSize(8, 4), new EditorPreview(0, 0, 8, 4), objects));
            using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "assets", "animations", "animations.json"));
            ScenePresenter presenter = new(AnimationLibrary.Load(stream), new Random(1));
            presenter.Update(level.World, [], 0.120);
            Entity grenade = level.GetEntity(2);
            SceneSprite[] layers = [.. presenter.Sprites.Where(sprite => sprite.Entity == grenade.Id)];
            SceneSprite body = Assert.Single(layers, sprite => !sprite.Additive && sprite.Page == "SpriteObjBombs");
            using SceneRenderer renderer = new(Path.Combine(AppContext.BaseDirectory, "assets", "sprites"));
            SceneCamera camera = new();
            camera.Snap(5, 1);
            using SKBitmap plain = Draw(renderer, camera, [body]);
            using SKBitmap glowing = Draw(renderer, camera, layers);
            using SKBitmap reversed = Draw(renderer, camera,
                [.. layers.Where(sprite => !sprite.Additive), .. layers.Where(sprite => sprite.Additive)]);
            int washedOut = 0;
            int compared = 0;
            int halo = 0;
            for (int y = 0; y < plain.Height; y++)
            {
                for (int x = 0; x < plain.Width; x++)
                {
                    SKColor pixel = plain.GetPixel(x, y);
                    if (pixel.Alpha == 255)
                    {
                        Assert.Equal(pixel, glowing.GetPixel(x, y));
                        compared++;
                        if (pixel != reversed.GetPixel(x, y))
                        {
                            washedOut++;
                        }
                    }
                    else if (pixel.Alpha == 0 && glowing.GetPixel(x, y) is { Red: > 0, Green: > 0 })
                    {
                        halo++;
                    }
                }
            }

            Assert.True(washedOut > 20);
            Assert.True(compared > 20);
            Assert.True(halo > 20);
        }

        private static SKBitmap Draw(SceneRenderer renderer, SceneCamera camera, IReadOnlyList<SceneSprite> sprites)
        {
            SKBitmap bitmap = new(160, 160);
            using SKCanvas canvas = new(bitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Translate(-320, -220);
            renderer.Draw(canvas, sprites, camera);
            canvas.Flush();
            return bitmap;
        }

        private static LevelObject Object(int id, string type, int x, int y)
        {
            return new LevelObject(id, type, 0, new LevelPosition(x, y), [], null);
        }
    }
}
