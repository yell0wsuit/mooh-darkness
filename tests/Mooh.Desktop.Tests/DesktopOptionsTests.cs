using System;

using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Desktop.Platform.Graphics;

using Xunit;

namespace Mooh.Desktop.Tests
{
    /// <summary>Verifies the desktop command line.</summary>
    public sealed class DesktopOptionsTests
    {
        /// <summary>Verifies no arguments start the first level on the standard difficulty.</summary>
        [Fact]
        public void NoArgumentsStartTheFirstLevel()
        {
            DesktopOptions options = DesktopOptions.Parse([]);

            Assert.Equal(0, options.LevelIndex);
            Assert.Same(DifficultySpeeds.Standard, options.Difficulty);
            Assert.Null(options.Renderer);
            Assert.Null(options.QuitAfterFrames);
            Assert.Null(options.ScreenshotPath);
        }

        /// <summary>Verifies Vulkan can be explicitly selected without enabling fallback.</summary>
        [Fact]
        public void VulkanCanBeSelected()
        {
            DesktopOptions options = DesktopOptions.Parse(["--renderer", "vulkan"]);

            Assert.Equal(GraphicsBackendKind.Vulkan, options.Renderer);
        }

        /// <summary>Verifies every option is read.</summary>
        [Fact]
        public void EveryOptionIsRead()
        {
            DesktopOptions options = DesktopOptions.Parse(
                ["--level", "3", "--difficulty", "2", "--renderer", "software", "--frames", "120", "--screenshot", "shot.png"]);

            Assert.Equal(2, options.LevelIndex);
            Assert.Same(DifficultySpeeds.Timed, options.Difficulty);
            Assert.Equal(GraphicsBackendKind.Software, options.Renderer);
            Assert.Equal(120, options.QuitAfterFrames);
            Assert.Equal("shot.png", options.ScreenshotPath);
            Assert.Equal(2.5, DesktopOptions.Parse(["--seconds", "2.5"]).QuitAfterSeconds);
        }

        /// <summary>
        /// Verifies a scripted input lists from when each set of keys is held: l, u, r, d for the
        /// directions and f for fire, nothing for no keys.
        /// </summary>
        [Fact]
        public void AScriptedInputIsRead()
        {
            DesktopOptions options = DesktopOptions.Parse(["--input", "2=r,3.5=rf,4="]);

            Assert.Equal(
                [
                    (2.0, new PlayerInputState(Left: false, Up: false, Right: true, Down: false)),
                    (3.5, new PlayerInputState(Left: false, Up: false, Right: true, Down: false, Fire: true)),
                    (4.0, default(PlayerInputState)),
                ],
                options.Script);
            Assert.Equal(new PlayerInputState(Left: false, Up: false, Right: true, Down: false), options.ScriptedInput(3));
            Assert.Equal(default, options.ScriptedInput(1));
        }

        /// <summary>Verifies an unknown or incomplete option is refused with a message.</summary>
        [Theory]
        [InlineData("--level")]
        [InlineData("--level", "0")]
        [InlineData("--renderer", "unknown")]
        [InlineData("--bogus")]
        [InlineData("--input", "2=x")]
        public void ABadOptionIsRefused(params string[] arguments)
        {
            _ = Assert.Throws<ArgumentException>(() => DesktopOptions.Parse(arguments));
        }
    }
}
