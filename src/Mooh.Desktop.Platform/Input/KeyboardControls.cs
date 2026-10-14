using System.Collections.Generic;

using Mooh.Core.Player;

using SDL3;

namespace Mooh.Desktop.Platform.Input
{
    /// <summary>
    /// Turns keyboard events into what the original's controls read: the held directions and fire
    /// key <c>CtrlKeyWork</c> polls, and the one-shot commands of <c>ModKeyUp</c> and the pause keys.
    /// </summary>
    /// <remarks>
    /// The arrows are <c>CNT_CtrlKeys</c>. The original's fire key is configurable; Space and both
    /// Ctrl keys serve here. A number key selects its weapon when released, <c>ModKeyUp</c> comparing
    /// against <c>CNT_GPrmWeapKey</c>, and Return switches to the next weapon as it is released,
    /// which is when <c>CtrlPool</c> sees <c>bSwitch</c> fall. Escape, P and Pause toggle the pause.
    /// </remarks>
    public sealed class KeyboardControls
    {
        /// <summary>
        /// The number keys from <c>CNT_GPrmWeapKey</c> in <c>WeapList</c> order. A table rather than
        /// a switch: the formatter's populate-switch fixer would expand one over every key code.
        /// </summary>
        private static readonly Dictionary<SDL.Keycode, WeaponKind> WeaponKeys = new()
        {
            [SDL.Keycode.Alpha1] = WeaponKind.Grenade,
            [SDL.Keycode.Alpha2] = WeaponKind.Rocket,
            [SDL.Keycode.Alpha3] = WeaponKind.Mine,
            [SDL.Keycode.Alpha4] = WeaponKind.BigBomb,
        };

        private readonly HashSet<SDL.Keycode> _held = [];
        private readonly List<ControlCommand> _commands = [];

        /// <summary>Gets the held directions and fire key.</summary>
        public PlayerInputState PlayerInput => new(
            Held(SDL.Keycode.Left),
            Held(SDL.Keycode.Up),
            Held(SDL.Keycode.Right),
            Held(SDL.Keycode.Down),
            Held(SDL.Keycode.Space) || Held(SDL.Keycode.LCtrl) || Held(SDL.Keycode.RCtrl));

        /// <summary>Records a key press.</summary>
        /// <param name="key">The key.</param>
        /// <param name="repeat">Whether the press is the system's key repeat.</param>
        public void KeyDown(SDL.Keycode key, bool repeat)
        {
            _ = _held.Add(key);
            if (!repeat && key is SDL.Keycode.Escape or SDL.Keycode.P or SDL.Keycode.Pause)
            {
                _commands.Add(ControlCommand.TogglePause);
            }
        }

        /// <summary>Records a key release.</summary>
        /// <param name="key">The key.</param>
        public void KeyUp(SDL.Keycode key)
        {
            if (!_held.Remove(key))
            {
                return;
            }

            if (key == SDL.Keycode.Return)
            {
                _commands.Add(ControlCommand.NextWeapon);
            }
            else if (WeaponOf(key) is WeaponKind weapon)
            {
                _commands.Add(ControlCommand.Select(weapon));
            }
        }

        /// <summary>Takes the commands given since the last call.</summary>
        /// <returns>The commands, in order.</returns>
        public IReadOnlyList<ControlCommand> TakeCommands()
        {
            ControlCommand[] commands = [.. _commands];
            _commands.Clear();
            return commands;
        }

        /// <summary>Releases every key without giving their commands, as when the window loses focus.</summary>
        public void ReleaseAll()
        {
            _held.Clear();
            _commands.Clear();
        }

        private static WeaponKind? WeaponOf(SDL.Keycode key)
        {
            return WeaponKeys.TryGetValue(key, out WeaponKind weapon) ? weapon : null;
        }

        private bool Held(SDL.Keycode key)
        {
            return _held.Contains(key);
        }
    }
}
