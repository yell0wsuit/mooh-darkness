using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Presentation.Sprites;
using Mooh.Presentation.Text;

namespace Mooh.Presentation.Screens
{
    /// <summary>The initialized panel composites used by the game shell.</summary>
    public enum MenuPanelKind
    {
        /// <summary>The main menu background.</summary>
        Main,
        /// <summary>The compact message background.</summary>
        Message,
        /// <summary>The high-score table background.</summary>
        HighScores,
        /// <summary>The completed-level statistics background.</summary>
        Statistics,
        /// <summary>The campaign completion background.</summary>
        Completion,
        /// <summary>The level introduction background.</summary>
        PreLevel
    }

    /// <summary>A button at a panel-relative point before group centering.</summary>
    /// <param name="Id">The action identifier.</param>
    /// <param name="Label">The localized label.</param>
    /// <param name="Tooltip">The localized tooltip.</param>
    /// <param name="X">The horizontal anchor.</param>
    /// <param name="Y">The vertical anchor.</param>
    /// <param name="Small">Whether to use a small button frame.</param>
    /// <param name="TabStop">Whether keyboard navigation selects this button.</param>
    public sealed record MenuItem(string Id, string Label, string Tooltip, double X, double Y, bool Small = false, bool TabStop = true);

    /// <summary>A button's final logical hit rectangle.</summary>
    /// <param name="Id">The action identifier.</param>
    /// <param name="Left">The left edge.</param>
    /// <param name="Top">The top edge.</param>
    /// <param name="Right">The right edge.</param>
    /// <param name="Bottom">The bottom edge.</param>
    public sealed record MenuButton(string Id, double Left, double Top, double Right, double Bottom);

    /// <summary>Lays out original panel tiles and buttons using their actual sprite and font bounds.</summary>
    /// <param name="page">The original menu sprite page.</param>
    /// <param name="font">The normal button font.</param>
    /// <param name="tooltipFont">The tooltip font, defaulting to the normal font when omitted.</param>
    /// <param name="random">The source of the original entry-bounce speeds.</param>
    public sealed class MenuPanelPresentation(SpritePage page, BitmapFont font, BitmapFont? tooltipFont = null, Random? random = null)
    {
        private readonly BitmapFont _tooltipFont = tooltipFont ?? font;
        private readonly Random _random = random ?? Random.Shared;
        private readonly List<PlacedButton> _buttons = [];
        private readonly List<OutgoingPanel> _outgoing = [];
        private string[] _tiles = [];
        private string _title = string.Empty;
        private double _titleY;
        private double _elapsed;
        private double _tooltipTime;
        private double _pointerX;
        private double _pointerY;
        private int _selected = -1;
        private int _pressed = -1;
        private int _hovered = -1;
        private double _frameMilliseconds = 1;

        /// <summary>Gets whether the panel has finished its 350 millisecond entry.</summary>
        public bool IsReady => _elapsed >= 0.35;

        /// <summary>Gets the current panel center.</summary>
        public double PanelY => -200 + (500 * Math.Min(1, _elapsed / 0.35));

        /// <summary>Gets the buttons' screen-space hit rectangles.</summary>
        public IReadOnlyList<MenuButton> Buttons => [.. _buttons.Select(b => new MenuButton(b.Item.Id, 400 + b.Left, PanelY + b.Top, 400 + b.Right, PanelY + b.Bottom))];

        /// <summary>Gets or sets panel-relative content drawn after its background and before its title and buttons.</summary>
        public IReadOnlyList<ScreenElement> Contents { get; set; } = [];

        /// <summary>Gets the live background anchor used by a button's attached rendering.</summary>
        /// <param name="id">The action identifier.</param>
        /// <returns>The button's current screen anchor.</returns>
        public (double X, double Y) GetButtonAnchor(string id)
        {
            PlacedButton button = _buttons.First(button => button.Item.Id == id);
            double shift = _pressed >= 0 && _buttons[_pressed].Item.Id == id ? 2 : 0;
            return (Math.Round(400 + button.X + button.Spring.X + shift), Math.Round(PanelY + button.Y + button.Spring.Y + shift));
        }

        /// <summary>Changes button text without rebuilding its geometry or restarting panel entry.</summary>
        /// <param name="id">The action identifier.</param>
        /// <param name="label">The replacement label, such as a checkbox glyph.</param>
        public void SetButtonLabel(string id, string label)
        {
            ArgumentNullException.ThrowIfNull(label);
            int index = _buttons.FindIndex(button => button.Item.Id == id);
            if (index < 0)
            {
                throw new KeyNotFoundException($"There is no menu button '{id}'.");
            }

            PlacedButton button = _buttons[index];
            _buttons[index] = button with
            {
                Item = button.Item with
                {
                    Label = label
                }
            };
        }

        /// <summary>Gets ordered screen draw commands.</summary>
        public IReadOnlyList<ScreenElement> Elements
        {
            get
            {
                List<ScreenElement> result = RenderPanel(interactive: true);
                foreach (OutgoingPanel outgoing in _outgoing)
                {
                    double shiftY = -500 * outgoing.Elapsed / 0.35;
                    result.AddRange(outgoing.Elements.Select(element => Translate(element, 0, Math.Round(shiftY))));
                }

                if (IsReady && _tooltipTime >= 0.75 - 1e-10 && _tooltipTime <= 10 + 1e-10 && _hovered >= 0 && _buttons[_hovered].Item.Tooltip.Length > 0)
                {
                    AddTooltip(result, _buttons[_hovered].Item.Tooltip);
                }
                return result;
            }
        }

        private List<ScreenElement> RenderPanel(bool interactive)
        {
            List<ScreenElement> result = [];
            if (_tiles.Length == 0)
            {
                return result;
            }
            result.AddRange(_tiles.Select(tile => new ScreenSprite("SpriteMenuPrt", tile, 400, Math.Round(PanelY))));
            // Panel contents share the panel's position and stay between its background and controls.
            result.AddRange(Contents.Select(element => Translate(element, 400, PanelY)));
            result.Add(new ScreenText(_title, 406, Math.Round(PanelY) + _titleY, "MenuFntTit"));
            for (int index = 0; index < _buttons.Count; index++)
            {
                PlacedButton button = _buttons[index];
                bool pressed = interactive && index == _pressed;
                string state = pressed ? "Prs" : interactive && index == _selected ? "Sel" : "Nrm";
                double shift = pressed ? 2 : 0;
                double x = Math.Round(400 + button.X + button.Spring.X + shift);
                double y = Math.Round(PanelY + button.Y + button.Spring.Y + shift);
                double textX = Math.Round(400 + button.TextX + button.Spring.X + shift);
                result.Add(new ScreenSprite("SpriteMenuPrt", button.Frame + "_" + state, x, y));
                if (state != "Nrm")
                {
                    result.Add(new ScreenText(button.Item.Label, textX, y, "MenuFntBtn" + state, TextAlignment.Left));
                }

                result.Add(new ScreenText(button.Item.Label, textX, y, "MenuFntBtnNrm", TextAlignment.Left));
            }

            return result;
        }

        private static ScreenElement Translate(ScreenElement element, double x, double y)
        {
            return element switch
            {
                ScreenImage image => image with { X = image.X + x, Y = image.Y + y },
                ScreenSprite sprite => sprite with { X = sprite.X + x, Y = sprite.Y + y },
                ScreenText text => text with { X = text.X + x, Y = text.Y + y },
                ScreenClip clip => clip with
                {
                    X = clip.X + x,
                    Y = clip.Y + y,
                    Elements = [.. clip.Elements.Select(child => Translate(child, x, y))],
                },
                _ => element,
            };
        }

        private void AddTooltip(List<ScreenElement> result, string text)
        {
            (int left, _, int right, _) = _tooltipFont.Bounds(text);
            List<(string Frame, double X)> tiles = [("OTTipLB", left)];
            double edge = left + page["OTTipLB"].PivotX + page["OTTipLB"].Width;
            while (right > edge)
            {
                tiles.Add(("OTTipC", edge));
                edge += page["OTTipC"].PivotX + page["OTTipC"].Width;
            }

            tiles.Add(("OTTipR", edge));
            edge += page["OTTipR"].PivotX + page["OTTipR"].Width;
            double x = Math.Min(_pointerX + 10, 800 - edge);
            double y = _pointerY - 45;
            result.AddRange(tiles.Select(tile => new ScreenSprite("SpriteMenuPrt", tile.Frame, x + tile.X, y)));
            result.Add(new ScreenText(text, x, y, "MenuFontToolTip", TextAlignment.Left));
        }

        /// <summary>Dismisses the panel and its outgoing transitions when leaving menu screens.</summary>
        public void Clear()
        {
            _tiles = [];
            _title = string.Empty;
            Contents = [];
            _buttons.Clear();
            _outgoing.Clear();
            _selected = _pressed = _hovered = -1;
            _elapsed = _tooltipTime = 0;
        }

        /// <summary>Starts a panel and centers its complete button group within the background bounds.</summary>
        /// <param name="kind">The original panel composite.</param>
        /// <param name="title">The localized title.</param>
        /// <param name="items">The buttons in navigation order.</param>
        /// <param name="centerX">The optional horizontal group offset.</param>
        /// <param name="centerY">The optional vertical group offset.</param>
        public void Show(MenuPanelKind kind, string title, IReadOnlyList<MenuItem> items, double? centerX = 0, double? centerY = -10)
        {
            ArgumentNullException.ThrowIfNull(items);
            if (_tiles.Length > 0)
            {
                _outgoing.Insert(0, new OutgoingPanel(RenderPanel(interactive: false)));
            }

            Contents = [];

            _title = title;
            _tiles = kind switch
            {
                MenuPanelKind.Main => Tiles("c_BackMain_", 12),
                MenuPanelKind.Message => Tiles("c_backMsg_", 12),
                MenuPanelKind.HighScores => [.. Tiles("c_BackBig_", 18), .. Tiles("c_BackHScore_", 18)],
                MenuPanelKind.Statistics => [.. Tiles("c_BackBig_", 18), .. Tiles("c_backLStat_", 18)],
                MenuPanelKind.Completion => [.. Tiles("c_BackBig_", 18), .. Tiles("c_BackCongr_", 18)],
                MenuPanelKind.PreLevel => [.. Tiles("c_BackBig_", 18), .. Tiles("c_backPreLvl_", 12)],
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };
            _titleY = kind == MenuPanelKind.Main ? -199 : kind == MenuPanelKind.Message ? -153 : kind == MenuPanelKind.HighScores ? -174 : -176;
            _elapsed = _tooltipTime = 0;
            _pressed = -1;
            _hovered = -1;
            _buttons.Clear();
            foreach (MenuItem item in items)
            {
                (int left, int top, int right, int bottom) = font.Bounds(item.Label);
                string prefix = item.Small ? "OBtnSm" : "OBtnNrm";
                string frameName = Enumerable.Range(1, item.Small ? 2 : 16)
                    .Select(n => prefix + n.ToString("00", System.Globalization.CultureInfo.InvariantCulture))
                    .MinBy(name => Math.Abs(page[name + "_Nrm"].Width - (right - left + 40)))!;
                SpriteFrame frame = page[frameName + "_Nrm"];
                double textX = item.X - Math.Floor((left + right) / 2.0);
                double shift = Math.Floor((frame.PivotX + Math.Floor(frame.Width / 2.0)) / 2.0);
                double x = item.X - shift;
                textX += shift;
                _buttons.Add(new PlacedButton(item, frameName, x, item.Y, textX,
                    Math.Min(textX + left, x + frame.PivotX) - 5,
                    Math.Min(item.Y + top, item.Y + frame.PivotY) - 5,
                    Math.Max(textX + right, x + frame.PivotX + frame.Width) + 5,
                    Math.Max(item.Y + bottom, item.Y + frame.PivotY + frame.Height) + 5));
            }
            if (_buttons.Count > 0)
            {
                double backX = (_tiles.Min(t => page[t].PivotX) + _tiles.Max(t => page[t].PivotX + page[t].Width)) / 2.0;
                double backY = (_tiles.Min(t => page[t].PivotY) + _tiles.Max(t => page[t].PivotY + page[t].Height)) / 2.0;
                double dx = centerX.HasValue ? backX - ((_buttons.Min(b => b.Left) + _buttons.Max(b => b.Right)) / 2) + centerX.Value : 0;
                double dy = centerY.HasValue ? backY - ((_buttons.Min(b => b.Top) + _buttons.Max(b => b.Bottom)) / 2) + centerY.Value : 0;
                for (int i = 0; i < _buttons.Count; i++)
                {
                    PlacedButton b = _buttons[i];
                    _buttons[i] = b with
                    {
                        X = b.X + dx,
                        Y = b.Y + dy,
                        TextX = b.TextX + dx,
                        Left = b.Left + dx,
                        Right = b.Right + dx,
                        Top = b.Top + dy,
                        Bottom = b.Bottom + dy
                    };
                }
            }
            _selected = _buttons.FindIndex(b => b.Item.TabStop);
        }

        /// <summary>Advances panel entry and tooltip clocks.</summary>
        /// <param name="seconds">The elapsed seconds.</param>
        public void Advance(double seconds)
        {
            if (!double.IsFinite(seconds) || seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            _frameMilliseconds = Math.Max(1, seconds * 1000);
            double remaining = seconds;
            while (remaining > 1e-10)
            {
                double step = Math.Min(0.025, remaining);
                AdvanceStep(step);
                remaining -= step;
            }
        }

        private void AdvanceStep(double seconds)
        {
            bool wasReady = IsReady;
            _elapsed += seconds;
            if (Math.Abs(_elapsed - 0.35) <= 1e-10)
            {
                _elapsed = 0.35;
            }

            _tooltipTime += seconds;
            foreach (OutgoingPanel panel in _outgoing)
            {
                panel.Elapsed += seconds;
            }

            _ = _outgoing.RemoveAll(panel => panel.Elapsed >= 0.35 - 1e-10);
            if (wasReady)
            {
                foreach (PlacedButton button in _buttons)
                {
                    button.Spring.Advance(seconds * 1000);
                }
            }
            else if (IsReady)
            {
                foreach (PlacedButton button in _buttons)
                {
                    double speed = 0.2 + (_random.NextDouble() * 0.2);
                    double tangent = -0.1 + (_random.NextDouble() * 0.2);
                    button.Spring.AddSpeed(Math.Sin(1.57) * tangent, speed + (Math.Cos(1.57) * tangent));
                }
            }
        }

        /// <summary>Moves keyboard selection, wrapping through selectable buttons.</summary>
        /// <param name="direction">Negative for previous, positive for next.</param>
        public void Navigate(int direction)
        {
            if (!IsReady || _buttons.Count == 0)
            {
                return;
            }

            for (int step = 0; step < _buttons.Count; step++)
            {
                _selected = (_selected + (direction < 0 ? -1 : 1) + _buttons.Count) % _buttons.Count;
                if (_buttons[_selected].Item.TabStop)
                {
                    break;
                }
            }
        }

        /// <summary>Selects the button beneath the logical pointer.</summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        public void MovePointer(double x, double y)
        {
            double dx = x - _pointerX;
            double dy = y - _pointerY;
            _pointerX = x;
            _pointerY = y;
            if (!IsReady)
            {
                return;
            }

            int selected = _buttons.FindIndex(b => x >= 400 + b.Left && x <= 400 + b.Right && y >= PanelY + b.Top && y <= PanelY + b.Bottom);
            if (selected != _hovered)
            {
                _tooltipTime = 0;
            }

            _selected = selected;
            _hovered = selected;
            if (selected >= 0)
            {
                _buttons[selected].Spring.AddSpeed(dx * 0.05 / _frameMilliseconds, dy * 0.05 / _frameMilliseconds);
            }
        }

        /// <summary>Arms the selected button on pointer press.</summary>
        public void Press()
        {
            if (IsReady)
            {
                _pressed = _selected;
                _tooltipTime = 10;
            }
        }

        /// <summary>Disarms a canceled gesture without activating the selected button.</summary>
        public void CancelPress()
        {
            _pressed = -1;
        }

        /// <summary>Invokes a button only when pointer release still selects the pressed button.</summary>
        /// <returns>The action identifier, or null.</returns>
        public string? Release()
        {
            string? result = _pressed >= 0 && _pressed == _selected ? Activate() : null;
            _pressed = -1;
            return result;
        }

        /// <summary>Invokes the selected button after panel entry.</summary>
        /// <returns>The action identifier, or null.</returns>
        public string? Activate()
        {
            return IsReady && _selected >= 0 ? _buttons[_selected].Item.Id : null;
        }

        private static string[] Tiles(string prefix, int count)
        {
            return [.. Enumerable.Range(0, count).Select(i => prefix + i.ToString("000", System.Globalization.CultureInfo.InvariantCulture))];
        }

        private sealed record PlacedButton(MenuItem Item, string Frame, double X, double Y, double TextX, double Left, double Top, double Right, double Bottom)
        {
            public ButtonSpring Spring { get; } = new();
        }

        private sealed class OutgoingPanel(IReadOnlyList<ScreenElement> elements)
        {
            public IReadOnlyList<ScreenElement> Elements { get; } = elements;
            public double Elapsed
            {
                get; set;
            }
        }

        private sealed class ButtonSpring
        {
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

            public void AddSpeed(double x, double y)
            {
                _speedX += x;
                _speedY += y;
            }

            public void Advance(double milliseconds)
            {
                double sine = Math.Sin(0.05 * milliseconds);
                double cosine = Math.Cos(0.05 * milliseconds);
                double fade = Math.Exp(-0.012 * milliseconds);
                (X, _speedX) = Step(X, _speedX, sine, cosine, fade);
                (Y, _speedY) = Step(Y, _speedY, sine, cosine, fade);
            }

            private static (double Offset, double Speed) Step(double offset, double speed, double sine, double cosine, double fade)
            {
                double nextOffset = Math.Clamp((offset * cosine) + (speed / 0.05 * sine), -10, 10);
                double nextSpeed = fade * ((((-0.012 * offset) + speed) * cosine) + (((-0.012 * speed / 0.05) - (0.05 * offset)) * sine));
                return Math.Abs(nextOffset) <= 0.5 && Math.Abs(nextSpeed) <= 0.001 ? (0, 0) : (nextOffset, nextSpeed);
            }
        }
    }
}
