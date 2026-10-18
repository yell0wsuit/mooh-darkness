using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Mooh.Presentation.Text;

namespace Mooh.Presentation.Hud
{
    /// <summary>
    /// The level texts, <c>TxtAdd</c> with <c>TxtTypeLvl</c>: every character with a glyph becomes
    /// its own <c>CTxtChar</c>, which drops in from above in turn, jiggles inside a small box at its
    /// place, and falls off the bottom when its hold ends.
    /// </summary>
    /// <remarks>
    /// <c>TxtTypeLvl</c> centers the lines on x 400 around y 250, 30 px apart, drops at 0.75 px/ms
    /// and spreads the characters' arrivals over 500 ms. The timer service, <c>TSrvTxt</c>, runs
    /// after the characters move, as <c>ModCycle</c> orders them.
    /// </remarks>
    internal sealed partial class LevelTexts(GameFont font)
    {
        private const double CenterX = 400;
        private const int CenterY = 250;
        private const int LineSpacing = 30;
        private const double DropSpeed = 0.75;
        private const int ArrivalMilliseconds = 500;
        private const double StartY = -20;
        private const double GoneY = 620;
        private const double JiggleBox = 4;

        private readonly List<Character> _characters = [];
        private readonly List<Timer> _timers = [];

        /// <summary>
        /// Shows a text for a hold, <c>TxtAdd</c>; every character already shown starts falling
        /// away, <c>TxtOutAll</c>.
        /// </summary>
        public void Add(string text, double holdMilliseconds)
        {
            foreach (Character shown in _characters)
            {
                _timers.Add(new Timer(0, shown, Arrive: null));
                shown.MoveOut();
            }

            if (text.Length == 0)
            {
                return;
            }

            int spacing = ArrivalMilliseconds / text.Length;
            string[] lines = LineBreak().Split(text);
            double y = CenterY - ((lines.Length - 1) / 2 * LineSpacing);
            int time = 0;
            foreach (string line in lines)
            {
                (int left, _, int right, _) = font.Bounds(line);
                double x = CenterX - ((right - left) / 2.0);
                foreach (char character in line)
                {
                    if (font.Has(character))
                    {
                        Character falling = new(character, x);
                        _characters.Add(falling);
                        _timers.Add(new Timer(time, falling, Arrive: y));
                        _timers.Add(new Timer(time + holdMilliseconds, falling, Arrive: null));
                    }

                    (double stepX, double stepY) = font.Advance(character);
                    x += stepX;
                    y += stepY;
                    time += spacing;
                }

                y += LineSpacing;
            }
        }

        /// <summary>Moves the characters, then runs the timers that are due.</summary>
        public void Advance(double milliseconds, Random random)
        {
            foreach (Character character in _characters)
            {
                character.Advance(milliseconds, random);
            }

            _ = _characters.RemoveAll(character => character.IsGone);
            for (int index = 0; index < _timers.Count; index++)
            {
                Timer timer = _timers[index];
                double left = timer.Milliseconds - milliseconds;
                if (left > 0)
                {
                    _timers[index] = timer with
                    {
                        Milliseconds = left
                    };
                    continue;
                }

                if (timer.Arrive is double line)
                {
                    timer.Character.MoveIn(line);
                }
                else
                {
                    timer.Character.MoveOut();
                }

                _timers.RemoveAt(index--);
            }
        }

        public void Draw(List<HudText> texts)
        {
            foreach (Character character in _characters)
            {
                if (character.IsShown)
                {
                    texts.Add(new HudText(
                        character.Glyph.ToString(),
                        Math.Floor(character.X),
                        Math.Floor(character.Y),
                        TextAlignment.Left,
                        HudPresenter.LevelFontName));
                }
            }
        }

        /// <summary>The line split of <c>re.split('[\n\r]+', Txt)</c>.</summary>
        [GeneratedRegex("[\n\r]+")]
        private static partial Regex LineBreak();

        /// <summary>A scheduled arrival, when it names a line, or departure.</summary>
        private readonly record struct Timer(double Milliseconds, Character Character, double? Arrive);

        /// <summary>One falling character, <c>CTxtChar</c>, with its out-of-box events.</summary>
        private sealed class Character(char glyph, double x)
        {
            private double _speedX;
            private double _speedY;
            private (double Left, double Top, double Right)? _box;
            private Bottom _bottom;
            private double _bottomY;

            private enum Bottom
            {
                None,
                Stop,
                Bounce,
                Gone,
            }

            public char Glyph { get; } = glyph;

            public double X { get; private set; } = x;

            public double Y { get; private set; } = StartY;

            public bool IsShown
            {
                get; private set;
            }

            public bool IsGone
            {
                get; private set;
            }

            /// <summary>Shows the character and drops it towards its line, <c>MoveIn</c>.</summary>
            public void MoveIn(double line)
            {
                IsShown = true;
                _speedY = DropSpeed;
                _speedX = 0;
                (_bottom, _bottomY) = (Bottom.Stop, line);
            }

            /// <summary>Drops the character off the screen, <c>MoveOut</c>.</summary>
            public void MoveOut()
            {
                _speedY = DropSpeed;
                (_bottom, _bottomY) = (Bottom.Gone, GoneY);
            }

            public void Advance(double milliseconds, Random random)
            {
                X += _speedX * milliseconds;
                Y += _speedY * milliseconds;
                if (_box is (double left, double top, double right))
                {
                    if (X < left)
                    {
                        _speedX = Math.Abs(_speedX);
                    }

                    if (Y < top)
                    {
                        _speedY = Math.Abs(_speedY);
                    }

                    if (X > right)
                    {
                        _speedX = -Math.Abs(_speedX);
                    }
                }

                if (_bottom == Bottom.None || Y <= _bottomY)
                {
                    return;
                }

                if (_bottom == Bottom.Stop)
                {
                    Stop(random);
                }
                else if (_bottom == Bottom.Bounce)
                {
                    _speedY = -Math.Abs(_speedY);
                }
                else
                {
                    IsGone = true;
                }
            }

            /// <summary>
            /// Settles on the line and starts the jiggle, <c>MoveStop</c>: a speed of 0.01 to
            /// 0.02 px/ms in a random direction, bounced inside 4 px of the place.
            /// </summary>
            private void Stop(Random random)
            {
                Y = _bottomY;
                double speed = 0.01 + (random.NextDouble() * 0.01);
                double angle = random.NextDouble() * 6.28;
                (_speedX, _speedY) = (speed * Math.Cos(angle), speed * Math.Sin(angle));
                _box = (X - JiggleBox, Y - JiggleBox, X + JiggleBox);
                (_bottom, _bottomY) = (Bottom.Bounce, Y + JiggleBox);
            }
        }
    }
}
