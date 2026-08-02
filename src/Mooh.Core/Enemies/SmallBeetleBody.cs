using System;

namespace Mooh.Core.Enemies
{
    /// <summary>Identifies and tracks a small beetle spawned by a defeated monster.</summary>
    public sealed class SmallBeetleBody
    {
        /// <summary>Initializes a spawned small beetle.</summary>
        /// <param name="color">The originating monster family.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="color"/> is undefined.</exception>
        public SmallBeetleBody(SmallBeetleColor color)
        {
            if (!Enum.IsDefined(color))
            {
                throw new ArgumentOutOfRangeException(nameof(color), color, null);
            }

            Color = color;
        }

        /// <summary>Gets the originating monster family.</summary>
        public SmallBeetleColor Color { get; }
    }
}
