using System;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// Places the world on the 800 by 600 screen as the original's game module does: the player
    /// sits at <c>CNT_WrdBase</c>, the screen center, and the world base eases toward that each
    /// cycle.
    /// </summary>
    /// <remarks>
    /// <c>GameMod.ModCycle</c> moves <c>fWrdBaseX</c> and <c>fWrdBaseY</c> with
    /// <c>ExpStiff(base, target, ulCTime * CNT_ScrPlFitK)</c>. <c>ExpStiff</c> is native; it is
    /// modelled here as exponential easing, which with <c>CNT_ScrPlFitK = -0.004</c> per millisecond
    /// has a 250 ms time constant. A cell is <c>CNT_WrdStepX</c> by <c>CNT_WrdStepY</c> pixels, and
    /// a frame's anchor is its object's cell coordinate scaled by them.
    /// </remarks>
    public sealed class SceneCamera
    {
        /// <summary>The original's <c>CNT_WrdStepX</c>, a cell's width in pixels.</summary>
        public const double CellWidth = 80;

        /// <summary>The original's <c>CNT_WrdStepY</c>, a cell's height in pixels.</summary>
        public const double CellHeight = 75;

        /// <summary>The original's <c>CNT_WrdBase</c>, where the player is drawn.</summary>
        private const double CenterX = 400;

        /// <summary>The original's <c>CNT_WrdBase</c>, where the player is drawn.</summary>
        private const double CenterY = 300;

        /// <summary>The original's <c>CNT_ScrPlFitK</c>, per millisecond.</summary>
        private const double FitPerMillisecond = -0.004;

        /// <summary>Gets or sets the additional visible width on each side of the original screen.</summary>
        public double HorizontalMargin
        {
            get; set;
        }

        /// <summary>Gets or sets the additional visible height above and below the original screen.</summary>
        public double VerticalMargin
        {
            get; set;
        }

        private double _baseX;
        private double _baseY;

        /// <summary>
        /// Gets the world base, <c>fWrdBaseX</c> and <c>fWrdBaseY</c> plus the jerk: where cell (0, 0)
        /// is drawn.
        /// </summary>
        public (double X, double Y) Base => (_baseX + Shake.X, _baseY + Shake.Y);

        /// <summary>
        /// Gets or sets the view's jerk, <c>fScrX</c> and <c>fScrY</c>, which moves everything the
        /// camera places but not the base it eases.
        /// </summary>
        public (double X, double Y) Shake
        {
            get; set;
        }

        /// <summary>Puts the target at the screen center at once, as a level start does.</summary>
        /// <param name="x">The target's column.</param>
        /// <param name="y">The target's row.</param>
        public void Snap(double x, double y)
        {
            (_baseX, _baseY) = BaseFor(x, y);
        }

        /// <summary>
        /// Moves the world base, as <c>WorldRotStopCBack</c> does when the grid turns so the rotator
        /// stays where it was drawn.
        /// </summary>
        /// <param name="x">The horizontal change in pixels.</param>
        /// <param name="y">The vertical change in pixels.</param>
        public void Shift(double x, double y)
        {
            _baseX += x;
            _baseY += y;
        }

        /// <summary>Eases the view toward the target.</summary>
        /// <param name="x">The target's column.</param>
        /// <param name="y">The target's row.</param>
        /// <param name="seconds">The elapsed time.</param>
        public void Follow(double x, double y, double seconds)
        {
            (double targetX, double targetY) = BaseFor(x, y);
            double keep = Math.Exp(seconds * 1000 * FitPerMillisecond);
            _baseX = targetX + ((_baseX - targetX) * keep);
            _baseY = targetY + ((_baseY - targetY) * keep);
        }

        /// <summary>Gets where a cell coordinate is drawn on the 800 by 600 screen.</summary>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <returns>The screen position in pixels.</returns>
        public (double X, double Y) ToScreen(double x, double y)
        {
            return (_baseX + Shake.X + (x * CellWidth), _baseY + Shake.Y + (y * CellHeight));
        }

        private static (double X, double Y) BaseFor(double x, double y)
        {
            return (CenterX - (x * CellWidth), CenterY - (y * CellHeight));
        }
    }
}
