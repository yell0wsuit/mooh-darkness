using System;
using System.Collections.Generic;
using System.Globalization;

namespace Mooh.Presentation.Screens
{
    /// <summary>Tracks the animated cursor used by the initialized menu screens.</summary>
    /// <remarks>The shipped configuration initializes no background ambient objects or logo overlay.</remarks>
    public sealed class MenuBackground
    {
        private double _framePosition;
        private bool _pressedAnimation;
        private bool _buttonHeld;

        /// <summary>Initializes menu presentation with the shared presentation random source.</summary>
        /// <param name="random">The presentation random source.</param>
        public MenuBackground(Random random)
        {
            ArgumentNullException.ThrowIfNull(random);
        }

        /// <summary>Gets the background overlays initialized by the shipped configuration.</summary>
        public IReadOnlyList<ScreenElement> Elements { get; } = [];

        /// <summary>Advances the current cursor animation, discarding overshoot at its end.</summary>
        /// <param name="seconds">The elapsed menu time.</param>
        public void Advance(double seconds)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(seconds);
            _framePosition += seconds * 1000 / (_pressedAnimation ? 35 : 50);
            if (_framePosition >= (_pressedAnimation ? 10 : 20))
            {
                _framePosition = 0;
                _pressedAnimation = false;
            }
        }

        /// <summary>Gets the cursor at the mouse position, starting its click animation on a new press.</summary>
        /// <param name="x">The mouse's horizontal screen coordinate.</param>
        /// <param name="y">The mouse's vertical screen coordinate.</param>
        /// <param name="pressed">Whether the mouse button is held.</param>
        /// <returns>The cursor's recovered atlas frame and position.</returns>
        public ScreenSprite Cursor(double x, double y, bool pressed)
        {
            if (pressed && !_buttonHeld && !_pressedAnimation)
            {
                _pressedAnimation = true;
                _framePosition = 0;
            }

            _buttonHeld = pressed;
            string prefix = _pressedAnimation ? "PCursorPrs" : "PCursor";
            return new ScreenSprite("SpriteMenuPrt", prefix + ((int)_framePosition).ToString("00", CultureInfo.InvariantCulture), x, y);
        }
    }
}
