using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>
    /// Brings up renderers in preference order and keeps the first that passes its validation
    /// frame, as contre-jour-dx and cuttherope-dx do.
    /// </summary>
    public static class BackendSelector
    {
        /// <summary>Gets the running platform's name: <c>macos</c>, <c>windows</c> or <c>linux</c>.</summary>
        /// <returns>The platform name.</returns>
        /// <exception cref="PlatformNotSupportedException">The platform has no desktop host.</exception>
        public static string CurrentPlatform()
        {
            return OperatingSystem.IsMacOS()
                ? "macos"
                : OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsLinux() ? "linux" : throw new PlatformNotSupportedException(RuntimeInformation.OSDescription);
        }

        /// <summary>Gets the renderers to try, best first. A forced renderer is the only candidate.</summary>
        /// <param name="platform">The platform name.</param>
        /// <param name="forced">The renderer the command line asked for, if any.</param>
        /// <returns>The candidates in order.</returns>
        /// <exception cref="PlatformNotSupportedException">The platform is unknown.</exception>
        public static GraphicsBackendKind[] PreferenceOrder(string platform, GraphicsBackendKind? forced)
        {
            return forced is GraphicsBackendKind only
                ? [only]
                : platform switch
                {
                    "macos" => [GraphicsBackendKind.Metal, GraphicsBackendKind.OpenGL, GraphicsBackendKind.Software],
                    "windows" or "linux" => [GraphicsBackendKind.OpenGL, GraphicsBackendKind.Software],
                    _ => throw new PlatformNotSupportedException(platform),
                };
        }

        /// <summary>Tries each candidate until one is created and validated.</summary>
        /// <typeparam name="T">The device type.</typeparam>
        /// <param name="order">The candidates, best first.</param>
        /// <param name="create">
        /// Builds one candidate, registering what it acquires with the lifetime as it goes.
        /// </param>
        /// <param name="validate">Draws and presents a frame, throwing if it cannot.</param>
        /// <returns>The first candidate that validated.</returns>
        /// <exception cref="AggregateException">Every candidate failed.</exception>
        public static GraphicsSelection<T> Attempt<T>(
            IReadOnlyList<GraphicsBackendKind> order,
            Func<GraphicsBackendKind, CandidateLifetime, T> create,
            Action<T> validate)
            where T : IDisposable
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(create);
            ArgumentNullException.ThrowIfNull(validate);
            List<RendererFailure> failures = [];
            foreach (GraphicsBackendKind kind in order)
            {
                CandidateLifetime lifetime = new();
                try
                {
                    T device = create(kind, lifetime);
                    validate(device);
                    return new GraphicsSelection<T>(kind, device, lifetime, failures.AsReadOnly());
                }
#pragma warning disable CA1031 // Any failure of a renderer passes it over for the next.
                catch (Exception failure)
#pragma warning restore CA1031
                {
                    failures.Add(new RendererFailure(kind, failure));
                    try
                    {
                        lifetime.Dispose();
                    }
                    catch (AggregateException cleanupFailure)
                    {
                        failures.Add(new RendererFailure(kind, cleanupFailure));
                    }
                }
            }

            throw new AggregateException(
                "No desktop renderer completed its validation frame. Passed over: "
                    + string.Join(", ", failures.Select(failure => $"{failure.Kind} ({failure.Failure.Message})")),
                failures.Select(failure => failure.Failure));
        }
    }

    /// <summary>Why one renderer was passed over, and which one it was.</summary>
    /// <param name="Kind">The renderer.</param>
    /// <param name="Failure">What went wrong.</param>
    public readonly record struct RendererFailure(GraphicsBackendKind Kind, Exception Failure);

    /// <summary>A validated device and every dependency acquired with it.</summary>
    /// <typeparam name="T">The device type.</typeparam>
    public sealed class GraphicsSelection<T> : IDisposable
        where T : IDisposable
    {
        private readonly CandidateLifetime _lifetime;

        internal GraphicsSelection(GraphicsBackendKind kind, T device, CandidateLifetime lifetime, IReadOnlyList<RendererFailure> failures)
        {
            Kind = kind;
            Device = device;
            _lifetime = lifetime;
            Failures = failures;
        }

        /// <summary>Gets the renderer.</summary>
        public GraphicsBackendKind Kind
        {
            get;
        }

        /// <summary>Gets the device.</summary>
        public T Device
        {
            get;
        }

        /// <summary>Gets the earlier candidates' failures, in attempt order.</summary>
        public IReadOnlyList<RendererFailure> Failures
        {
            get;
        }

        /// <summary>Releases the device and everything acquired with it.</summary>
        public void Dispose()
        {
            _lifetime.Dispose();
        }
    }
}
