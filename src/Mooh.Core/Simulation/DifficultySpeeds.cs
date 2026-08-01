namespace Mooh.Core.Simulation
{
    /// <summary>
    /// Provides the movement-speed profiles recovered from the original difficulty table.
    /// </summary>
    public sealed record DifficultySpeeds
    {
        private const double DifficultyZeroMultiplier = 0.75;
        private const double StandardMonsterSpeed = 1.5625;
        private const double StandardNormalSpeed = 1.875;
        private const double StandardPlayerSpeed = 2.5;

        private DifficultySpeeds(
            double playerSpeed,
            double normalSpeed,
            double monsterSpeed)
        {
            PlayerSpeed = playerSpeed;
            NormalSpeed = normalSpeed;
            MonsterSpeed = monsterSpeed;
        }

        /// <summary>Gets the profile shared by the standard difficulty levels.</summary>
        public static DifficultySpeeds Standard { get; } = new(
            StandardPlayerSpeed,
            StandardNormalSpeed,
            StandardMonsterSpeed);

        /// <summary>Gets the profile for difficulty zero.</summary>
        public static DifficultySpeeds DifficultyZero { get; } = new(
            StandardPlayerSpeed * DifficultyZeroMultiplier,
            StandardNormalSpeed * DifficultyZeroMultiplier,
            StandardMonsterSpeed * DifficultyZeroMultiplier);

        /// <summary>Gets the player walking speed in cells per second.</summary>
        public double PlayerSpeed { get; }

        /// <summary>Gets the normal object speed in cells per second.</summary>
        public double NormalSpeed { get; }

        /// <summary>Gets the monster movement speed in cells per second.</summary>
        public double MonsterSpeed { get; }
    }
}
