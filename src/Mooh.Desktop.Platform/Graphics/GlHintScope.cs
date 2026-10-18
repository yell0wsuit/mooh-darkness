using System;
using System.Collections.Generic;

using SDL3;

namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>
    /// Sets SDL hints for one renderer attempt and puts back what was there before. SDL's hints are
    /// process-wide, so a value left behind would belong to whichever renderer is tried next.
    /// </summary>
    public sealed class GlHintScope : IDisposable
    {
        private readonly List<(string Name, string? Previous)> _displaced = [];
        private bool _disposed;

        /// <summary>Sets a hint until the scope ends.</summary>
        /// <param name="name">The hint.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="InvalidOperationException">SDL refused the hint.</exception>
        public void Set(string name, string value)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrEmpty(name);
            string? previous = SDL.GetHint(name);
            if (!SDL.SetHint(name, value))
            {
                throw new InvalidOperationException($"SDL would not set the hint '{name}': {SDL.GetError()}");
            }

            _displaced.Add((name, previous));
        }

        /// <summary>Restores every hint the scope set.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int index = _displaced.Count - 1; index >= 0; index--)
            {
                (string name, string? previous) = _displaced[index];
                _ = previous is null ? SDL.ResetHint(name) : SDL.SetHint(name, previous);
            }

            _displaced.Clear();
        }
    }
}
