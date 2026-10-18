using System.Linq;

using Mooh.Presentation.Animation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Verifies blasts play the recovered <c>SfxExplAdd1x1</c> layers.</summary>
    public sealed class ExplosionEffectTests
    {
        /// <summary>Verifies the five recovered <c>SfxExpl</c> layers load with their numbers.</summary>
        [Fact]
        public void TheFiveExplosionLayersLoad()
        {
            EffectLayer[] layers = [.. SceneHarness.Library.Explosion];

            Assert.Equal(5, layers.Length);
            Assert.All(layers, layer => Assert.Equal("SpriteExpl", layer.Page));
            Assert.Equal((35, 50), layers[0].FrameMilliseconds);
            Assert.Equal([3, 5], layers[2].Counts);
            Assert.Equal(4, layers[2].Variants.Count);
            Assert.Equal((-0.4, 0.4), layers[4].Shift);
            Assert.Equal([1, 1, 1, 0, 0], layers.Select(layer => layer.Pass));
        }

        /// <summary>Verifies an open blast cell plays the layers around it, then they go.</summary>
        [Fact]
        public void ABlastPlaysItsLayersThenGoes()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 5, 1)));
            harness.Step(0.016);
            harness.Detonate(harness.Level.GetEntity(200));

            harness.Step(0.016);

            SceneSprite[] blast = [.. harness.Presenter.Sprites.Where(sprite => sprite.Page == "SpriteExpl")];
            Assert.InRange(blast.Length, 1 + 1 + 3 + 1 + 3, (1 + 1 + 5 + 1 + 5) * 9);
            Assert.All(blast, sprite => Assert.InRange(sprite.X, 3.5, 6.5));

            harness.StepUntil(() => !harness.Presenter.Sprites.Any(sprite => sprite.Page == "SpriteExpl"), limitSeconds: 3);
        }

        /// <summary>Verifies a cell where something survived the blast shows none, as <c>_ObjExplWrd</c> tests.</summary>
        [Fact]
        public void ASurvivingCellShowsNoBlast()
        {
            SceneHarness harness = new(SceneHarness.BuildLevel(1, ("WBomb", 5, 1)));
            harness.Step(0.016);
            harness.Detonate(harness.Level.GetEntity(200));

            harness.Step(0.016);

            // The metal floor under the bomb survives, so its row shows no blast; the open rows
            // above do, each layer within its recovered shift of the cell.
            SceneSprite[] blast = [.. harness.Presenter.Sprites.Where(sprite => sprite.Page == "SpriteExpl")];
            Assert.NotEmpty(blast);
            Assert.All(blast, sprite => Assert.True(sprite.Y < 1.5, $"A blast drew at row {sprite.Y}."));
        }
    }
}
