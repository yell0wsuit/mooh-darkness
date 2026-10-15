using System;

namespace Mooh.Presentation.Hud
{
    /// <summary>
    /// One screen panel, the recovered <c>CPnlInfo</c>: three frames — normal, lit and dim — at a
    /// fixed place, and its value 20 px inside and 30 px below, left- or right-aligned.
    /// </summary>
    internal sealed class HudPanel(string normal, string lit, string dim, int x, int y, bool textRight)
    {
        /// <summary>The panel's jerk, <c>CNT_ScrPnlStiffSh</c>.</summary>
        private const double JerkDistance = 6;

        public string Normal { get; } = normal;

        public string Lit { get; } = lit;

        public string Dim { get; } = dim;

        public int X { get; } = x;

        public int Y { get; } = y;

        public bool TextRight { get; } = textRight;

        public PanelSpring Spring { get; } = new();

        /// <summary>The text's point, <c>TPos</c>, which does not jerk with the frame.</summary>
        public (int X, int Y) TextPoint => (X + (TextRight ? -20 : 20), Y + 30);

        /// <summary>
        /// Jerks the panel, <c>JerkMe</c>: a 6 px push in a direction drawn from
        /// <c>App.Rand.FLim(6.28)</c>.
        /// </summary>
        public void Jerk(Random random)
        {
            double angle = random.NextDouble() * 6.28;
            Spring.Push(JerkDistance * Math.Cos(angle), JerkDistance * Math.Sin(angle));
        }
    }
}
