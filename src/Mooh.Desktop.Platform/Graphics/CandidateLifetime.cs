using System;
using System.Collections.Generic;

namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>
    /// Holds everything one device candidate acquired and releases it in reverse order. Each
    /// acquisition is registered right after it succeeds, so a failure part-way through releases
    /// exactly what exists.
    /// </summary>
    public sealed class CandidateLifetime : IDisposable
    {
        private readonly Stack<IDisposable> _resources = new();
        private bool _disposed;

        /// <summary>Registers a resource to release with the candidate.</summary>
        /// <typeparam name="T">The resource type.</typeparam>
        /// <param name="resource">The resource.</param>
        /// <returns><paramref name="resource"/>.</returns>
        /// <exception cref="ObjectDisposedException">The lifetime has ended.</exception>
        public T Own<T>(T resource)
            where T : IDisposable
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(resource);
            _resources.Push(resource);
            return resource;
        }

        /// <summary>Releases everything, even when some releases throw, then reports them together.</summary>
        /// <exception cref="AggregateException">A release failed.</exception>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            List<Exception> failures = [];
            while (_resources.TryPop(out IDisposable? resource))
            {
                try
                {
                    resource.Dispose();
                }
#pragma warning disable CA1031 // A failed release must not stop the rest; all are reported together.
                catch (Exception failure)
#pragma warning restore CA1031
                {
                    failures.Add(failure);
                }
            }

            if (failures.Count != 0)
            {
                throw new AggregateException("Releasing a graphics candidate failed.", failures);
            }
        }
    }
}
