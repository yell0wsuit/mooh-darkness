using System;

namespace Mooh.SimulationRunner
{
    /// <summary>Reports invalid scenario source data.</summary>
    public sealed class ScenarioValidationException : Exception
    {
        /// <summary>Initializes a scenario validation failure.</summary>
        /// <param name="source">The source name used in diagnostics.</param>
        /// <param name="message">The validation failure detail.</param>
        public ScenarioValidationException(string source, string message)
            : base($"{source}: {message}")
        {
            SourceName = source;
        }

        /// <summary>Initializes a scenario validation failure with an underlying exception.</summary>
        /// <param name="source">The source name used in diagnostics.</param>
        /// <param name="message">The validation failure detail.</param>
        /// <param name="innerException">The underlying source error.</param>
        public ScenarioValidationException(string source, string message, Exception innerException)
            : base($"{source}: {message}", innerException)
        {
            SourceName = source;
        }

        /// <summary>Gets the source name used in diagnostics.</summary>
        public string SourceName { get; }
    }
}
