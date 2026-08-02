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
            double hardShakeDamage,
            double explosionDamage,
            double foodHeal,
            double beetleHeal,
            bool isTimed)
        {
            IsTimed = isTimed;
            PlayerSpeed = playerSpeed;
            NormalSpeed = normalSpeed;
            MonsterSpeed = monsterSpeed;
            ShakeDamage = shakeDamage;
            HardShakeDamage = hardShakeDamage;
            ExplosionDamage = explosionDamage;
            FoodHeal = foodHeal;
            BeetleHeal = beetleHeal;
        }

        /// <summary>Gets the standard non-timed difficulty profile.</summary>
        public static DifficultySpeeds Standard { get; } = new(
            StandardPlayerSpeed,
            StandardNormalSpeed,
            StandardMonsterSpeed,
            20,
            30,
            20,
            40,
            2,
            false);

        /// <summary>Gets the profile for difficulty zero.</summary>
        public static DifficultySpeeds DifficultyZero { get; } = new(
            StandardPlayerSpeed * DifficultyZeroMultiplier,
            StandardNormalSpeed * DifficultyZeroMultiplier,
            StandardMonsterSpeed * DifficultyZeroMultiplier,
            10,
            15,
            10,
            40,
            2,
            false);

        /// <summary>Gets the timed difficulty profile.</summary>
        public static DifficultySpeeds Timed { get; } = new(
            StandardPlayerSpeed,
            StandardNormalSpeed,
            StandardMonsterSpeed,
            20,
            30,
            20,
            15,
            1,
            true);

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

        /// <summary>Gets player damage caused by an explosion.</summary>
        public double ExplosionDamage { get; }

        /// <summary>Gets the health restored by eating one food object.</summary>
        public double FoodHeal { get; }

        /// <summary>
        /// Gets the health restored by absorbing one small beetle. The recovered
        /// <c>Compl.HSmall</c> is <c>100.0 / 50.0</c> on both untimed profiles and drops to 1 on
        /// the timed profile.
        /// </summary>
        public double BeetleHeal { get; }

        /// <summary>
        /// Gets whether the profile replaces the health axis with a countdown clock. The original
        /// bypasses the health loss condition entirely on this profile, redirecting damage and
        /// healing to the remaining play time instead. The clock itself is not yet implemented.
        /// </summary>
        public bool IsTimed { get; }
    }
}
