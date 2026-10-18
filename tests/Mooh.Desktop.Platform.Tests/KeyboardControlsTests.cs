using Mooh.Core.Player;
using Mooh.Desktop.Platform.Input;

using SDL3;

using Xunit;

namespace Mooh.Desktop.Platform.Tests
{
    /// <summary>
    /// Verifies the keyboard reaches the game as the original's <c>CtrlKeyWork</c> and
    /// <c>ModKeyUp</c> read it.
    /// </summary>
    public sealed class KeyboardControlsTests
    {
        /// <summary>Verifies any held key can skip construction, including keys outside the player controls.</summary>
        [Fact]
        public void AnyHeldKeyCanSkipConstruction()
        {
            KeyboardControls controls = new();
            Assert.False(controls.AnyKeyHeld);
            controls.KeyDown(SDL.Keycode.A, repeat: false);
            Assert.True(controls.AnyKeyHeld);
            Assert.Equal(default, controls.PlayerInput);
            controls.KeyUp(SDL.Keycode.A);
            Assert.False(controls.AnyKeyHeld);
            controls.KeyDown(SDL.Keycode.Return, repeat: false);
            controls.ReleaseAll();
            Assert.False(controls.AnyKeyHeld);
        }

        /// <summary>Verifies held arrows and the fire key become the player's held input.</summary>
        [Fact]
        public void HeldKeysBecomeThePlayersInput()
        {
            KeyboardControls controls = new();

            controls.KeyDown(SDL.Keycode.Left, repeat: false);
            controls.KeyDown(SDL.Keycode.Space, repeat: false);

            Assert.Equal(new PlayerInputState(Left: true, Up: false, Right: false, Down: false, Fire: true), controls.PlayerInput);

            controls.KeyUp(SDL.Keycode.Left);
            Assert.Equal(new PlayerInputState(Left: false, Up: false, Right: false, Down: false, Fire: true), controls.PlayerInput);
        }

        /// <summary>Verifies a number key selects its weapon when it is released, as <c>ModKeyUp</c> does.</summary>
        [Fact]
        public void ANumberKeySelectsItsWeaponOnRelease()
        {
            KeyboardControls controls = new();

            controls.KeyDown(SDL.Keycode.Alpha3, repeat: false);
            Assert.Empty(controls.TakeCommands());

            controls.KeyUp(SDL.Keycode.Alpha3);
            Assert.Equal([ControlCommand.Select(WeaponKind.Mine)], controls.TakeCommands());
            Assert.Empty(controls.TakeCommands());
        }

        /// <summary>
        /// Verifies Return switches to the next weapon when released: <c>CtrlPool</c> calls
        /// <c>WeapNext</c> as <c>bSwitch</c> falls.
        /// </summary>
        [Fact]
        public void ReturnSwitchesToTheNextWeaponOnRelease()
        {
            KeyboardControls controls = new();

            controls.KeyDown(SDL.Keycode.Return, repeat: false);
            controls.KeyDown(SDL.Keycode.Return, repeat: true);
            controls.KeyUp(SDL.Keycode.Return);

            Assert.Equal([ControlCommand.NextWeapon], controls.TakeCommands());
        }

        /// <summary>Verifies Escape, P and Pause toggle the pause, once per press.</summary>
        [Fact]
        public void PauseKeysTogglePauseOncePerPress()
        {
            KeyboardControls controls = new();

            controls.KeyDown(SDL.Keycode.Escape, repeat: false);
            controls.KeyDown(SDL.Keycode.Escape, repeat: true);
            controls.KeyDown(SDL.Keycode.P, repeat: false);

            Assert.Equal([ControlCommand.TogglePause, ControlCommand.TogglePause], controls.TakeCommands());
        }

        /// <summary>Verifies losing focus releases every held key, so nothing stays pressed.</summary>
        [Fact]
        public void ReleasingAllClearsTheHeldInput()
        {
            KeyboardControls controls = new();
            controls.KeyDown(SDL.Keycode.Right, repeat: false);
            controls.KeyDown(SDL.Keycode.Return, repeat: false);

            controls.ReleaseAll();

            Assert.Equal(default, controls.PlayerInput);
            Assert.Empty(controls.TakeCommands());
        }
    }
}
