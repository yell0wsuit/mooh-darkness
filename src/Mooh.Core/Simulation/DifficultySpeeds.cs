using System;

namespace Mooh.Core.Simulation
{
    /// <summary>Provides gameplay values recovered from the original difficulty table.</summary>
    public sealed record DifficultySpeeds
    {
        private const double DifficultyZeroMultiplier = 0.75;
        private const double StandardMonsterSpeed = 1.5625;
        private const double StandardNormalSpeed = 1.875;
        private const double StandardPlayerSpeed = 2.5;

        /// <summary>
        /// The recovered <c>CNT_GPrmHealthSp</c>, <c>100 / (15 * 60 * 1000)</c> per millisecond,
        /// restated per second.
        /// </summary>
        private const double RecoveredHealthDrainPerSecond = 100.0 / (15 * 60);

        private DifficultySpeeds(
            double playerSpeed,
            double normalSpeed,
            double monsterSpeed,
            double shakeDamage,
            double hardShakeDamage,
            double explosionDamage,
            double fireDamage,
            double foodHeal,
            double beetleHeal,
            bool isTimed)
        {
            IsTimed = isTimed;
            HealthDrainPerSecond = isTimed ? 0 : RecoveredHealthDrainPerSecond;
            PlayerSpeed = playerSpeed;
            NormalSpeed = normalSpeed;
            MonsterSpeed = monsterSpeed;
            ShakeDamage = shakeDamage;
            HardShakeDamage = hardShakeDamage;
            ExplosionDamage = explosionDamage;
            FireDamage = fireDamage;
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
            30,
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
            40,
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

        /// <summary>
        /// Gets player damage caused by walking into fire. The recovered <c>Compl.HFire</c> is
        /// 30 on difficulty zero and 40 on both standard profiles, so unlike the shake and blast
        /// values it does not simply halve.
        /// </summary>
        public double FireDamage { get; }

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
        /// healing to the remaining play time instead.
        /// </summary>
        public bool IsTimed { get; }

        /// <summary>
        /// Gets the passive health drain in health per second. The recovered <c>Game.ModCycle</c>
        /// drains a full bar in fifteen minutes outside timed play and not at all on the timed
        /// profile, whose clock replaces health. A host may set it to zero to isolate other
        /// behavior from the drain.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
        public double HealthDrainPerSecond
        {
            get;
            init => field = double.IsFinite(value) && value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
