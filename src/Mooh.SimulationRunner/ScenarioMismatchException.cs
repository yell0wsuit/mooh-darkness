using System;

namespace Mooh.SimulationRunner
{
    /// <summary>Reports a mismatch between expected and observed scenario behavior.</summary>
    /// <param name="source">The scenario source name.</param>
    /// <param name="path">The expectation path that differs.</param>
    /// <param name="detail">The expected-versus-actual diagnostic.</param>
    public sealed class ScenarioMismatchException(string source, string path, string detail)
        : Exception($"{source}: {path}: {detail}")
    {
        /// <summary>Gets the scenario source name.</summary>
        public string SourceName { get; } = source;

        /// <summary>Gets the expectation path that differs.</summary>
        public string Path { get; } = path;
    }
}
