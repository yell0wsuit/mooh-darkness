namespace Mooh.Core.Simulation
{
    /// <summary>Provides gameplay values recovered from the original difficulty table.</summary>
    public sealed record DifficultySpeeds
    {
        private const double DifficultyZeroMultiplier = 0.75;
        private const double StandardMonsterSpeed = 1.5625;
        private const double StandardNormalSpeed = 1.875;
        private const double StandardPlayerSpeed = 2.5;

        private DifficultySpeeds(
            double playerSpeed,
            double normalSpeed,
            double monsterSpeed,
            double shakeDamage,
            double hardShakeDamage)
        {
            PlayerSpeed = playerSpeed;
            NormalSpeed = normalSpeed;
            MonsterSpeed = monsterSpeed;
            ShakeDamage = shakeDamage;
            HardShakeDamage = hardShakeDamage;
        }

        /// <summary>Gets the standard non-timed difficulty profile.</summary>
        public static DifficultySpeeds Standard { get; } = new(
            StandardPlayerSpeed,
            StandardNormalSpeed,
            StandardMonsterSpeed,
            20,
            30);

        /// <summary>Gets the profile for difficulty zero.</summary>
        public static DifficultySpeeds DifficultyZero { get; } = new(
            StandardPlayerSpeed * DifficultyZeroMultiplier,
            StandardNormalSpeed * DifficultyZeroMultiplier,
            StandardMonsterSpeed * DifficultyZeroMultiplier,
            10,
            15);

        /// <summary>Gets the timed difficulty profile.</summary>
        public static DifficultySpeeds Timed { get; } = new(
            StandardPlayerSpeed,
            StandardNormalSpeed,
            StandardMonsterSpeed,
            20,
            30);

        /// <summary>Gets the player walking speed in cells per second.</summary>
        public double PlayerSpeed { get; }

        /// <summary>Gets the normal object speed in cells per second.</summary>
        public double NormalSpeed { get; }

        /// <summary>Gets the monster movement speed in cells per second.</summary>
        public double MonsterSpeed { get; }

        /// <summary>Gets player damage caused by an ordinary falling impact.</summary>
        public double ShakeDamage { get; }

        /// <summary>Gets player damage caused by a hard falling impact.</summary>
        public double HardShakeDamage { get; }
    }
}
