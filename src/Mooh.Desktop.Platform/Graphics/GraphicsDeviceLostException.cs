using System;

namespace Mooh.Desktop.Platform.Graphics
{
    /// <summary>The graphics device stopped being usable while the game was running.</summary>
    public sealed class GraphicsDeviceLostException : Exception
    {
        /// <summary>Initializes the exception.</summary>
        public GraphicsDeviceLostException()
        {
        }

        /// <summary>Initializes the exception with a message.</summary>
        /// <param name="message">What happened.</param>
        public GraphicsDeviceLostException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes the exception with a message and its cause.</summary>
        /// <param name="message">What happened.</param>
        /// <param name="innerException">The cause.</param>
        public GraphicsDeviceLostException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
