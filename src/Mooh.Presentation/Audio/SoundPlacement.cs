using System;

namespace Mooh.Presentation.Audio
{
    /// <summary>
    /// How loud a sound plays in each ear, from where it happens on the 800 by 600 screen, as the
    /// original's <c>ObjSnd</c> works it out.
    /// </summary>
    /// <remarks>
    /// <c>CNT_SndDist</c> keeps full volume within 300 px of the screen center and falls linearly
    /// to 0.2 at 900 px. <c>CNT_SndLFunc</c> falls from 1 at the left edge to 0 at 1000 px, and
    /// <c>CNT_SndRFunc</c> rises from 0 at -200 px to 1 at the right edge, so the two cross at 0.6 in
    /// the middle.
    /// </remarks>
    /// <param name="Left">The left gain, with the distance applied.</param>
    /// <param name="Right">The right gain, with the distance applied.</param>
    /// <param name="Distance">The distance falloff alone.</param>
    public readonly record struct SoundPlacement(double Left, double Right, double Distance)
    {
        /// <summary>Gets the placement of a sound that is not in the world, such as a menu click.</summary>
        public static SoundPlacement Centered { get; } = new(1, 1, 1);

        /// <summary>Places a sound at a screen position.</summary>
        /// <param name="x">The screen column in pixels.</param>
        /// <param name="y">The screen row in pixels.</param>
        /// <returns>The placement.</returns>
        public static SoundPlacement At(double x, double y)
        {
            double fromCenter = Math.Sqrt(((x - 400) * (x - 400)) + ((y - 300) * (y - 300)));
            double distance = Fuzzy(fromCenter, (0, 1), (300, 1), (900, 0.2));
            return new SoundPlacement(
                Fuzzy(x, (0, 1), (1000, 0)) * distance,
                Fuzzy(x, (-200, 0), (800, 1)) * distance,
                distance);
        }

        /// <summary>
        /// The original's <c>FuzzyBase.FFunc</c>: linear between the points, held flat beyond the
        /// first and last.
        /// </summary>
        private static double Fuzzy(double value, params ReadOnlySpan<(double X, double Y)> points)
        {
            if (value <= points[0].X)
            {
                return points[0].Y;
            }

            for (int index = 1; index < points.Length; index++)
            {
                (double x0, double y0) = points[index - 1];
                (double x1, double y1) = points[index];
                if (value <= x1)
                {
                    return y0 + ((value - x0) / (x1 - x0) * (y1 - y0));
                }
            }

            return points[^1].Y;
        }
    }
}
