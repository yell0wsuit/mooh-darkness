using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Simulation;
using Mooh.Presentation.Scene;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>Checks directional-arrow flight against the original animation and trail contract.</summary>
    public sealed class ArrowPresentationTests
    {
        /// <summary>Flight repeats its ten even-numbered frames and rotation sound every 250 milliseconds.</summary>
        [Theory]
        [InlineData("OArrowL", "Left")]
        [InlineData("OArrowT", "Top")]
        [InlineData("OArrowR", "Right")]
        [InlineData("OArrowB", "Bottom")]
        public void FlightAnimationAndRotationSoundRepeat(string type, string direction)
        {
            (SceneHarness harness, Entity arrow) = Launch(type);
            Assert.Equal("OFly" + direction + "00", harness.Animator(arrow).Frame);
            harness.Step(0.25);
            Assert.False(harness.Animator(arrow).Animation.IsHeld);
            Assert.Equal("OFly" + direction + "00", harness.Animator(arrow).Frame);
            _ = Assert.Single(harness.Presenter.Sounds, sound => sound.Alias == "FlyRot");
            harness.Step(0.025);
            Assert.Equal("OFly" + direction + "02", harness.Animator(arrow).Frame);
            Assert.DoesNotContain(harness.Presenter.Sounds, sound => sound.Alias == "FlyRot");
            harness.Step(0.225);
            Assert.Equal("OFly" + direction + "00", harness.Animator(arrow).Frame);
            _ = Assert.Single(harness.Presenter.Sounds, sound => sound.Alias == "FlyRot");
        }

        /// <summary>Each completed flight cycle emits five normal-blended particles moving behind the arrow.</summary>
        [Theory]
        [InlineData("OArrowL", 1, 0)]
        [InlineData("OArrowT", 0, 1)]
        [InlineData("OArrowR", -1, 0)]
        [InlineData("OArrowB", 0, -1)]
        public void TrailEmitsBehindTheArrowAndDriftsBackward(string type, int backX, int backY)
        {
            (SceneHarness harness, Entity arrow) = Launch(type);
            Assert.Empty(Trails(harness));
            harness.Step(0.25);
            List<SceneSprite> particles = Trails(harness);
            Assert.Equal(5, particles.Count);
            (double x, double y) = DrawnPosition.Of(arrow);
            Assert.All(particles, particle =>
            {
                double dx = particle.X - x;
                double dy = particle.Y - y;
                Assert.InRange((dx * backX) + (dy * backY), 0.19, 0.4);
                Assert.InRange(Math.Abs((dx * backY) - (dy * backX)), 0, 0.12);
                Assert.False(particle.Additive);
                Assert.Equal(1, particle.Pass);
                Assert.Matches("^PFly[12]_01$", particle.Frame);
            });
            harness.Step(0.05);
            List<SceneSprite> drifted = Trails(harness);
            Assert.Equal(5, drifted.Count);
            // Render sorting can reorder particles as they cross cell boundaries.
            double driftX = drifted.Sum(p => p.X) - particles.Sum(p => p.X);
            double driftY = drifted.Sum(p => p.Y) - particles.Sum(p => p.Y);
            Assert.InRange((driftX * backX) + (driftY * backY), 0.49, 0.75);
            Assert.All(drifted, particle => Assert.Matches("^PFly[12]_0[13]$", particle.Frame));
        }

        /// <summary>The flight-start sound occurs once, after the windup rather than at activation.</summary>
        [Fact]
        public void FlightStartsWithLaunchAndRotationSounds()
        {
            (SceneHarness harness, _) = Launch("OArrowR");
            _ = Assert.Single(harness.Presenter.Sounds, sound => sound.Alias == "FlyFly");
            _ = Assert.Single(harness.Presenter.Sounds, sound => sound.Alias == "FlyRot");
            harness.Step(0.25);
            Assert.DoesNotContain(harness.Presenter.Sounds, sound => sound.Alias == "FlyFly");
        }

        /// <summary>Pausing cannot emit more trails and existing trails end after their five frames.</summary>
        [Fact]
        public void TrailsPauseAndExpireAfterTheArrowIsGone()
        {
            (SceneHarness harness, Entity arrow) = Launch("OArrowR");
            harness.Step(0.25);
            List<SceneSprite> before = Trails(harness);
            Assert.Equal(5, before.Count);
            harness.Presenter.Update(harness.Level.World, [], 0, bonusSeconds: 1);
            Assert.Equal(before, Trails(harness));
            Assert.True(harness.Level.World.Remove(arrow.Id));
            harness.Presenter.Update(harness.Level.World, [], 0.501);
            Assert.Empty(Trails(harness));
        }

        private static (SceneHarness Harness, Entity Arrow) Launch(string type)
        {
            LevelDocument level = SceneHarness.BuildLevel(1, (type, 16, 16)) with
            {
                Size = new LevelSize(32, 32)
            };
            SceneHarness harness = new(level);
            Entity arrow = harness.Level.GetEntity(200);
            harness.Step(0);
            List<SimulationEvent> events = [];
            harness.Level.Simulation.LaunchArrow(arrow, events);
            harness.Presenter.Update(harness.Level.World, events, 0);
            Assert.Empty(harness.Presenter.Sounds);
            Assert.Empty(Trails(harness));
            harness.Step(0.245);
            Assert.True(arrow.FlyingArrowBody!.IsFlying);
            return (harness, arrow);
        }

        private static List<SceneSprite> Trails(SceneHarness harness)
        {
            return [.. harness.Presenter.Sprites.Where(sprite => sprite.Page == "SpriteParts" && sprite.Frame.StartsWith("PFly", StringComparison.Ordinal))];
        }
    }
}
