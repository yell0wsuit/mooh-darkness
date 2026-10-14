using System;
using System.Collections.Generic;

using Mooh.Desktop.Platform.Graphics;

using Xunit;

namespace Mooh.Desktop.Platform.Tests
{
    /// <summary>Verifies renderers are tried best first and a failed one releases what it acquired.</summary>
    public sealed class BackendSelectorTests
    {
        /// <summary>Verifies each platform's preference order, ending in the software renderer.</summary>
        [Theory]
        [InlineData("macos", new[] { GraphicsBackendKind.Metal, GraphicsBackendKind.OpenGL, GraphicsBackendKind.Software })]
        [InlineData("windows", new[] { GraphicsBackendKind.OpenGL, GraphicsBackendKind.Software })]
        [InlineData("linux", new[] { GraphicsBackendKind.OpenGL, GraphicsBackendKind.Software })]
        public void EachPlatformTriesItsBestRendererFirst(string platform, GraphicsBackendKind[] expected)
        {
            Assert.Equal(expected, BackendSelector.PreferenceOrder(platform, forced: null));
        }

        /// <summary>Verifies a forced renderer is the only candidate.</summary>
        [Fact]
        public void AForcedRendererIsTheOnlyCandidate()
        {
            Assert.Equal([GraphicsBackendKind.Software], BackendSelector.PreferenceOrder("macos", GraphicsBackendKind.Software));
        }

        /// <summary>
        /// Verifies a candidate that fails validation is released, with everything it acquired,
        /// and the next one is kept.
        /// </summary>
        [Fact]
        public void AFailedCandidateIsReleasedAndTheNextKept()
        {
            List<string> released = [];

            using GraphicsSelection<Probe> selection = BackendSelector.Attempt(
                [GraphicsBackendKind.Metal, GraphicsBackendKind.Software],
                (kind, lifetime) =>
                {
                    _ = lifetime.Own(new Release(() => released.Add($"{kind} resource")));
                    return lifetime.Own(new Probe(kind, () => released.Add($"{kind} device")));
                },
                probe =>
                {
                    if (probe.Kind == GraphicsBackendKind.Metal)
                    {
                        throw new InvalidOperationException("no drawable");
                    }
                });

            Assert.Equal(GraphicsBackendKind.Software, selection.Kind);
            Assert.Equal(["Metal device", "Metal resource"], released);
            RendererFailure failure = Assert.Single(selection.Failures);
            Assert.Equal(GraphicsBackendKind.Metal, failure.Kind);
        }

        /// <summary>Verifies running out of candidates names every one that was passed over.</summary>
        [Fact]
        public void RunningOutOfCandidatesNamesThemAll()
        {
            AggregateException failure = Assert.Throws<AggregateException>(() => BackendSelector.Attempt<Probe>(
                [GraphicsBackendKind.OpenGL],
                (_, _) => throw new InvalidOperationException("no context"),
                _ => { }));

            Assert.Contains("OpenGL (no context)", failure.Message, StringComparison.Ordinal);
        }

        private sealed class Probe(GraphicsBackendKind kind, Action release) : IDisposable
        {
            public GraphicsBackendKind Kind { get; } = kind;

            public void Dispose()
            {
                release();
            }
        }

        private sealed class Release(Action release) : IDisposable
        {
            public void Dispose()
            {
                release();
            }
        }
    }
}
