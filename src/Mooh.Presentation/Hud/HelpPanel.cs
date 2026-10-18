using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Mooh.Presentation.Hud
{
    /// <summary>
    /// The help panel at the top of the screen, <c>HlpAdd</c>, <c>HlpCycle</c> and
    /// <c>HlpRender</c>: a new text slides in from above over 250 ms while the old one slides out,
    /// rests for five seconds, then slides away.
    /// </summary>
    internal sealed partial class HelpPanel
    {
        /// <summary><c>CNT_HlpInTime</c>.</summary>
        private const int SlideMilliseconds = 250;

        /// <summary><c>CNT_HlpShTime</c>.</summary>
        private const int ShowMilliseconds = 5000;

        /// <summary><c>CNT_HlpOutX</c>.</summary>
        private const int CenterX = 400;

        /// <summary><c>CNT_HlpOutY</c>.</summary>
        private const int TopY = 40;

        /// <summary><c>CNT_HlpOutShY</c>.</summary>
        private const int LineSpacing = 28;

        /// <summary><c>CNT_HlpOutOutY</c>.</summary>
        private const int OffScreen = 50;

        private bool _showing;
        private double _milliseconds;
        private string? _name;
        private string[] _shown = [];
        private string[] _coming = [];

        /// <summary>Takes a new text, <c>HlpAdd</c>; the same text while shown stays five seconds more.</summary>
        public void Add(string id, string? text)
        {
            if (_name != id || !_showing)
            {
                _name = id;
                _coming = text is null ? [] : LineBreak().Split(text);
                if (_showing)
                {
                    _showing = false;
                    _milliseconds = SlideMilliseconds;
                }
            }
            else
            {
                _milliseconds = ShowMilliseconds;
            }
        }

        /// <summary>Moves the panel on, <c>HlpCycle</c>.</summary>
        public void Advance(double milliseconds)
        {
            _milliseconds -= milliseconds;
            if (_milliseconds >= 0)
            {
                return;
            }

            if (!_showing)
            {
                _showing = true;
                _milliseconds = ShowMilliseconds;
                _shown = _coming;
                _coming = [];
            }
            else if (_shown.Length > 0)
            {
                _showing = false;
                _name = null;
                _coming = [];
                _milliseconds = SlideMilliseconds;
            }
        }

        /// <summary>
        /// Lays out the lines, <c>HlpRender</c>, in whole pixels as its integer arithmetic does.
        /// </summary>
        public void Draw(List<HudText> texts)
        {
            int left = (int)Math.Max(0, _milliseconds);
            int y = TopY;
            if (!_showing)
            {
                y -= (OffScreen + (_coming.Length * LineSpacing)) * left / SlideMilliseconds;
                foreach (string line in _coming)
                {
                    texts.Add(new HudText(line, CenterX, y, Text.TextAlignment.Center));
                    y += LineSpacing;
                }

                y = TopY - ((OffScreen + (_shown.Length * LineSpacing)) * (SlideMilliseconds - left) / SlideMilliseconds);
            }

            foreach (string line in _shown)
            {
                texts.Add(new HudText(line, CenterX, y, Text.TextAlignment.Center));
                y += LineSpacing;
            }
        }

        /// <summary>The line split of <c>re.split('[\r\n]+', Txt)</c>.</summary>
        [GeneratedRegex("[\r\n]+")]
        private static partial Regex LineBreak();
    }
}
