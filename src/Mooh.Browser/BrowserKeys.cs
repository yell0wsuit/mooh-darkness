using System.Collections.Generic;

using SDL3;

namespace Mooh.Browser
{
    /// <summary>Matches host-events.js key ids to the shared desktop input contract.</summary>
    internal static class BrowserKeys
    {
        internal static readonly IReadOnlyDictionary<int, SDL.Keycode> Values = new Dictionary<int, SDL.Keycode>
        {
            [1] = SDL.Keycode.Escape,
            [2] = SDL.Keycode.Return,
            [3] = SDL.Keycode.Left,
            [4] = SDL.Keycode.Up,
            [5] = SDL.Keycode.Right,
            [6] = SDL.Keycode.Down,
            [7] = SDL.Keycode.Space,
            [8] = SDL.Keycode.LCtrl,
            [9] = SDL.Keycode.RCtrl,
            [10] = SDL.Keycode.Alpha1,
            [11] = SDL.Keycode.Alpha2,
            [12] = SDL.Keycode.Alpha3,
            [13] = SDL.Keycode.Alpha4,
            [14] = SDL.Keycode.P,
            [15] = SDL.Keycode.Pause,
            [16] = SDL.Keycode.F2,
            [17] = SDL.Keycode.F10,
            [18] = SDL.Keycode.Backspace,
            [19] = SDL.Keycode.Delete,
            [20] = SDL.Keycode.Home,
            [21] = SDL.Keycode.End,
            [22] = SDL.Keycode.Tab,
            [23] = SDL.Keycode.LShift,
            [24] = SDL.Keycode.RShift,
        };
    }
}
