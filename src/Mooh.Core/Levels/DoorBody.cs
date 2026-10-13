namespace Mooh.Core.Levels
{
    /// <summary>
    /// Tracks the recovered start door, <c>GameTypeDoor</c>, which <c>GamePlayer.GameInit</c> places
    /// in the player's cell. Unused, it closes and is killed once the player is out; used as a
    /// shelter, it stays for the rest of the level.
    /// </summary>
    public sealed class DoorBody
    {
        /// <summary>
        /// Gets the time left until an unused door is gone, the recovered <c>ST_DoorCloseAndOut</c>
        /// and <c>ST_DoorOut</c>, or <see langword="null"/> while the door is not leaving.
        /// </summary>
        public double? RemovalRemainingSeconds
        {
            get; internal set;
        }

        /// <summary>Gets whether the door has served as a shelter and so stays for good.</summary>
        public bool IsShelter
        {
            get; internal set;
        }
    }
}
