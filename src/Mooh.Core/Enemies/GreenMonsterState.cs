namespace Mooh.Core.Enemies
{
    /// <summary>Identifies the current green-monster behavior phase.</summary>
    public enum GreenMonsterState
    {
        /// <summary>The monster is ready to choose its next action.</summary>
        Deciding,

        /// <summary>The monster is walking toward an adjacent cell.</summary>
        Walking,

        /// <summary>The monster is turning clockwise.</summary>
        Turning,
    }
}
