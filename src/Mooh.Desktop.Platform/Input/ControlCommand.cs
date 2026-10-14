using Mooh.Core.Player;

namespace Mooh.Desktop.Platform.Input
{
    /// <summary>Identifies a one-shot command the keyboard gives the game.</summary>
    public enum ControlCommandKind
    {
        /// <summary>Make a weapon current, the recovered <c>WeapCurrSet</c>.</summary>
        SelectWeapon,

        /// <summary>Switch to the next weapon, the recovered <c>WeapNext</c>.</summary>
        NextWeapon,

        /// <summary>Pause or resume the game.</summary>
        TogglePause,
    }

    /// <summary>A one-shot keyboard command.</summary>
    /// <param name="Kind">The command.</param>
    /// <param name="Weapon">The weapon to select, for <see cref="ControlCommandKind.SelectWeapon"/>.</param>
    public readonly record struct ControlCommand(ControlCommandKind Kind, WeaponKind? Weapon = null)
    {
        /// <summary>Gets the next-weapon command.</summary>
        public static ControlCommand NextWeapon { get; } = new(ControlCommandKind.NextWeapon);

        /// <summary>Gets the pause command.</summary>
        public static ControlCommand TogglePause { get; } = new(ControlCommandKind.TogglePause);

        /// <summary>Gets the command selecting a weapon.</summary>
        /// <param name="weapon">The weapon.</param>
        /// <returns>The command.</returns>
        public static ControlCommand Select(WeaponKind weapon)
        {
            return new(ControlCommandKind.SelectWeapon, weapon);
        }
    }
}
