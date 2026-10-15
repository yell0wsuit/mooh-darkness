using System;

namespace Mooh.Presentation.Hud
{
    /// <summary>
    /// The damped spring a panel jerks on, the native stiffness of <c>StiffSetK</c> and
    /// <c>StiffSetPos</c>.
    /// </summary>
    /// <remarks>
    /// The native solver, <c>sub_497560</c>, keeps the angular frequency, its inverse, the decay,
    /// and the sine, cosine and exponential of a step, and moves an offset and its speed along
    /// <c>x(t) = e^(λt)(x cos ωt + v/ω sin ωt)</c>. A panel uses <c>CNT_ScrPnlStiffK</c>:
    /// ω = 0.05 per millisecond and λ = −0.012 per millisecond. The spring comes to rest once both
    /// are negligible, so a whole-pixel draw position does not hang one pixel off.
    /// </remarks>
    internal sealed class PanelSpring
    {
        private const double Frequency = 0.05;
        private const double Decay = -0.012;
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

        /// <summary>Pushes the panel off its place, <c>StiffSetPos</c>.</summary>
        public void Push(double x, double y)
        {
            X += x;
            Y += y;
        }

        public void Advance(double milliseconds)
        {
            double sine = Math.Sin(Frequency * milliseconds);
            double cosine = Math.Cos(Frequency * milliseconds);
            double fade = Math.Exp(Decay * milliseconds);
            (X, _speedX) = Step(X, _speedX, sine, cosine, fade);
            (Y, _speedY) = Step(Y, _speedY, sine, cosine, fade);
            if (Math.Abs(X) < RestOffset && Math.Abs(Y) < RestOffset
                && Math.Abs(_speedX) < RestSpeed && Math.Abs(_speedY) < RestSpeed)
            {
                (X, Y, _speedX, _speedY) = (0, 0, 0, 0);
            }
        }

        private static (double Offset, double Speed) Step(double offset, double speed, double sine, double cosine, double fade)
        {
            return (
                fade * ((offset * cosine) + (speed / Frequency * sine)),
                fade * ((((Decay * offset) + speed) * cosine) + (((Decay * speed / Frequency) - (Frequency * offset)) * sine)));
        }
    }
}
