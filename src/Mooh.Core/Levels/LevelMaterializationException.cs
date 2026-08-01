using System;

namespace Mooh.Core.Levels
{
    /// <summary>Represents a failure at the explicit archival-to-runtime boundary.</summary>
    public sealed class LevelMaterializationException : Exception
    {
        /// <summary>Initializes a materialization failure.</summary>
        /// <param name="message">The contextual failure message.</param>
        public LevelMaterializationException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a materialization failure caused by another exception.</summary>
        /// <param name="message">The contextual failure message.</param>
        /// <param name="innerException">The underlying catalog or model failure.</param>
        public LevelMaterializationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
