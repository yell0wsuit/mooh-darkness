using System.Collections.Generic;

using Mooh.Presentation.Animation;

using Xunit;

namespace Mooh.Presentation.Tests
{
    /// <summary>
    /// Verifies the original's playback rules: a clip advances one frame per frame time, a state
    /// that ends into itself wraps, a state change restarts at the first frame, and a gameplay end
    /// handler the presentation cannot run holds the last frame.
    /// </summary>
    public sealed class AnimationPlayerTests
    {
        /// <summary>Verifies the frame advances once per recovered frame time.</summary>
        [Fact]
        public void TheFrameAdvancesOncePerFrameTime()
        {
            AnimationPlayer player = Start("loop");

            player.Advance(0.024);
            Assert.Equal("a0", player.Frame);

            player.Advance(0.002);
            Assert.Equal("a1", player.Frame);
        }

        /// <summary>Verifies a state whose end is itself wraps, keeping the overshoot.</summary>
        [Fact]
        public void AStateEndingIntoItselfWraps()
        {
            AnimationPlayer player = Start("loop");

            player.Advance(0.080);

            Assert.Equal("loop", player.State);
            Assert.Equal("a0", player.Frame);
            Assert.Equal(0.2, player.Position, 6);
        }

        /// <summary>Verifies a state with no end at all loops too, as the native list does.</summary>
        [Fact]
        public void AStateWithNoEndLoops()
        {
            AnimationPlayer player = Start("endless");

            player.Advance(0.160);

            Assert.Equal("endless", player.State);
            Assert.Equal("e1", player.Frame);
        }

        /// <summary>Verifies a state ending into another restarts that one at its first frame.</summary>
        [Fact]
        public void AStateChangeRestartsAtTheFirstFrame()
        {
            AnimationPlayer player = Start("intro");

            player.Advance(0.120);

            Assert.Equal("loop", player.State);
            Assert.Equal(0, player.Position);
            Assert.Equal("a0", player.Frame);
        }

        /// <summary>Verifies an unresolved gameplay handler holds the last frame.</summary>
        [Fact]
        public void AnUnresolvedHandlerHoldsTheLastFrame()
        {
            AnimationPlayer player = Start("strike");

            player.Advance(1.0);

            Assert.Equal("strike", player.State);
            Assert.True(player.IsHeld);
            Assert.Equal("s1", player.Frame);
        }

        /// <summary>Verifies a handler the rules resolve continues into the state they name.</summary>
        [Fact]
        public void AResolvedHandlerContinuesIntoItsState()
        {
            AnimationPlayer player = Start("strike", handler => handler == "_AEnd_Str" ? "loop" : null);

            player.Advance(0.050);

            Assert.Equal("loop", player.State);
            Assert.False(player.IsHeld);
        }

        /// <summary>Verifies a state with no frames draws nothing for one frame time, then ends.</summary>
        [Fact]
        public void AFramelessStateLastsOneFrameTime()
        {
            AnimationPlayer player = Start("hidden");

            player.Advance(0.299);
            Assert.Null(player.Frame);
            Assert.Equal("hidden", player.State);

            player.Advance(0.002);
            Assert.Equal("intro", player.State);
        }

        /// <summary>Verifies a still clip, with no frame time, never ends.</summary>
        [Fact]
        public void AStillClipNeverEnds()
        {
            AnimationPlayer player = Start("still");

            player.Advance(100);

            Assert.Equal("still", player.State);
            Assert.Equal("t0", player.Frame);
        }

        /// <summary>Verifies re-entering the playing state keeps its position.</summary>
        [Fact]
        public void ReenteringThePlayingStateKeepsItsPosition()
        {
            AnimationPlayer player = Start("loop");
            player.Advance(0.030);

            player.Play("loop");

            Assert.Equal("a1", player.Frame);
        }

        private static AnimationPlayer Start(string state, System.Func<string, string?>? handlers = null)
        {
            AnimationTable table = new(
                "Page",
                new Dictionary<string, AnimationClip>
                {
                    ["loop"] = Clip(["a0", "a1", "a2"], 25, next: "loop"),
                    ["endless"] = Clip(["e0", "e1"], 50),
                    ["intro"] = Clip(["i0", "i1"], 50, next: "loop"),
                    ["strike"] = Clip(["s0", "s1"], 20, handler: "_AEnd_Str"),
                    ["hidden"] = new AnimationClip(null, [], null, [300], "intro", null, null),
                    ["still"] = Clip(["t0"], 0),
                });
            AnimationPlayer player = new(table, new TestRules(handlers));
            player.Play(state);
            return player;
        }

        private static AnimationClip Clip(string[] frames, int milliseconds, string? next = null, string? handler = null)
        {
            return new AnimationClip(null, [frames], null, [milliseconds], next, handler, null);
        }

        private sealed class TestRules(System.Func<string, string?>? handlers) : IAnimationRules
        {
            public IReadOnlyList<IReadOnlyList<string>> Candidates(string state, AnimationClip clip)
            {
                return clip.Variants;
            }

            public IReadOnlyList<string> PickVariant(IReadOnlyList<IReadOnlyList<string>> candidates)
            {
                return candidates.Count == 0 ? [] : candidates[0];
            }

            public double FrameMilliseconds(AnimationClip clip)
            {
                return clip.FrameMilliseconds[0];
            }

            public double StartPosition(AnimationClip clip, IReadOnlyList<string> frames)
            {
                return 0;
            }

            public string? ResolveHandler(string handler)
            {
                return handlers?.Invoke(handler);
            }
        }
    }
}
