using System;

namespace Mooh.Presentation
{
    /// <summary>
    /// The native damped spring that panels and the view jerk on: <c>StiffSetK</c> with
    /// <c>StiffSetPos</c> for a panel, <c>StiffCalc</c> for the view.
    /// </summary>
    /// <remarks>
    /// The spring solver keeps the angular frequency, its inverse, the decay,
    /// and the sine, cosine and exponential of a step, and moves an offset and its speed along
    /// <c>x(t) = e^(λt)(x cos ωt + v/ω sin ωt)</c>, both in milliseconds. The spring comes to rest
    /// once both are negligible, so a whole-pixel draw position does not hang one pixel off.
    /// </remarks>
    /// <param name="frequency">The angular frequency ω, per millisecond.</param>
    /// <param name="decay">The decay λ, per millisecond, negative.</param>
    internal sealed class DampedSpring(double frequency, double decay)
    {
        private const double RestOffset = 0.01;
        private const double RestSpeed = 0.001;

        private double _speedX;
        private double _speedY;

        public double X
        {
            get; private set;
        }

        public double Y
        {
            get; private set;
        }

        /// <summary>Pushes the offset further, <c>StiffSetPos</c>.</summary>
        public void Push(double x, double y)
        {
            X += x;
            Y += y;
        }

        /// <summary>Puts the offset somewhere, keeping its speed, as <c>ModScrStiff</c> does.</summary>
        public void Set(double x, double y)
        {
            X = x;
            Y = y;
        }

        public void Advance(double milliseconds)
        {
            double sine = Math.Sin(frequency * milliseconds);
            double cosine = Math.Cos(frequency * milliseconds);
            double fade = Math.Exp(decay * milliseconds);
            (X, _speedX) = Step(X, _speedX, sine, cosine, fade);
            (Y, _speedY) = Step(Y, _speedY, sine, cosine, fade);
            if (Math.Abs(X) < RestOffset && Math.Abs(Y) < RestOffset
                && Math.Abs(_speedX) < RestSpeed && Math.Abs(_speedY) < RestSpeed)
            {
                (X, Y, _speedX, _speedY) = (0, 0, 0, 0);
            }
        }

        private (double Offset, double Speed) Step(double offset, double speed, double sine, double cosine, double fade)
        {
            return (
                fade * ((offset * cosine) + (speed / frequency * sine)),
                fade * ((((decay * offset) + speed) * cosine) + (((decay * speed / frequency) - (frequency * offset)) * sine)));
        }
    }
}
