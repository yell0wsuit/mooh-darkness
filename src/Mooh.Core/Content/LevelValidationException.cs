using System;

namespace Mooh.Core.Content
{
    /// <summary>Reports invalid archival level content.</summary>
    public sealed class LevelValidationException : Exception
    {
        /// <summary>Initializes a validation exception with a descriptive message.</summary>
        /// <param name="message">The validation failure.</param>
        public LevelValidationException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a validation exception caused by another exception.</summary>
        /// <param name="message">The validation failure.</param>
        /// <param name="innerException">The underlying error.</param>
        public LevelValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
