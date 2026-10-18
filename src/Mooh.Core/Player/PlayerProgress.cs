using System;

using Mooh.Core.Combat;
using Mooh.Core.Levels;

namespace Mooh.Core.Player
{
    /// <summary>Stores recovered score, treasure, inventory, key, and timed-bonus state.</summary>
    public sealed class PlayerProgress
    {
        /// <summary>
        /// CBonInfo.BonOn adds the recovered 45 seconds to any unused duration, rather than
        /// replacing the timer when the same bonus is collected again.
        /// </summary>
        private static readonly TimeSpan BonusDuration = TimeSpan.FromSeconds(45);
        /// <summary>The recovered <c>WeapValReset</c> stock: five grenades and nothing else.</summary>
        private readonly int[] _ammo = [5, 0, 0, 0];
        private int _keyMask;

        /// <summary>Gets the level score earned by collection and progression.</summary>
        public int Score
        {
            get; private set;
        }

        /// <summary>
        /// Gets the treasure eaten while the bonus was still available, the recovered
        /// <c>ulGameTreasGet</c>.
        /// </summary>
        public int TreasureCollected
        {
            get; private set;
        }

        /// <summary>
        /// Gets the treasure the bonus asks for, the recovered <c>ulGameTreasAll</c>: what the
        /// player could reach at level start, raised if more is eaten than that.
        /// </summary>
        public int TotalTreasure
        {
            get; private set;
        }

        /// <summary>
        /// Gets whether the all-treasure bonus can still be earned, the recovered
        /// <c>bGameHaveTreas</c>. It is lost for good once any treasure is killed other than by
        /// eating, and once the bonus is paid.
        /// </summary>
        public bool IsTreasureBonusAvailable
        {
            get; private set;
        }

        /// <summary>Gets whether the all-treasure bonus has been paid.</summary>
        public bool HasEarnedTreasureBonus
        {
            get; private set;
        }

        /// <summary>
        /// Gets the weapon the fire control uses, the recovered <c>WeapCurr</c>. The recovered
        /// <c>WeapValReset</c> starts every level on the grenade.
        /// </summary>
        public WeaponKind CurrentWeapon
        {
            get; private set;
        }

        /// <summary>Gets the ammunition held for a weapon.</summary>
        /// <param name="weapon">The weapon.</param>
        /// <returns>The rounds held.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="weapon"/> is undefined.</exception>
        public int AmmoOf(WeaponKind weapon)
        {
            return Enum.IsDefined(weapon)
                ? _ammo[(int)weapon]
                : throw new ArgumentOutOfRangeException(nameof(weapon), weapon, null);
        }

        /// <summary>Gets the available grenade count, including the recovered five starting grenades.</summary>
        public int Grenades => AmmoOf(WeaponKind.Grenade);

        /// <summary>Gets the available rocket count.</summary>
        public int Rockets => AmmoOf(WeaponKind.Rocket);

        /// <summary>Gets the available mine count.</summary>
        public int Mines => AmmoOf(WeaponKind.Mine);

        /// <summary>Gets the available big-bomb count.</summary>
        public int BigBombs => AmmoOf(WeaponKind.BigBomb);

        /// <summary>Gets the remaining cola protection duration.</summary>
        public TimeSpan ColaRemaining
        {
            get; private set;
        }

        /// <summary>Gets the remaining monster-freezing ice duration.</summary>
        public TimeSpan IceRemaining
        {
            get; private set;
        }

        /// <summary>Determines whether a numbered colored key has been collected.</summary>
        /// <param name="keyNumber">The key number in the inclusive range 1 through 3.</param>
        /// <returns>Whether the player owns that key.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="keyNumber"/> is outside 1 through 3.</exception>
        public bool HasKey(int keyNumber)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(keyNumber, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(keyNumber, 3);
            return (_keyMask & (1 << (keyNumber - 1))) != 0;
        }

        /// <summary>Starts the bonus from the recovered <c>GameInit</c> trace count.</summary>
        internal void StartTreasureTrace(int reachableTreasure)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(reachableTreasure);
            TotalTreasure = reachableTreasure;
            IsTreasureBonusAvailable = reachableTreasure > 0;
        }

        /// <summary>
        /// Records an eaten treasure, the recovered <c>CtrlCellEat</c> branch, and pays the
        /// 5000-point bonus when the trace finds none left.
        /// </summary>
        /// <param name="noneLeft">Whether no other treasure remains reachable.</param>
        /// <returns>The bonus score awarded, or zero.</returns>
        internal int EatTreasure(bool noneLeft)
        {
            if (!IsTreasureBonusAvailable)
            {
                return 0;
            }

            TreasureCollected++;
            TotalTreasure = Math.Max(TotalTreasure, TreasureCollected);
            if (!noneLeft)
            {
                return 0;
            }

            const int TreasureBonus = 5000;
            IsTreasureBonusAvailable = false;
            HasEarnedTreasureBonus = true;
            Score += TreasureBonus;
            return TreasureBonus;
        }

        /// <summary>
        /// Forfeits the bonus, the recovered <c>Game.ObjKill</c> branch for a treasure that dies
        /// other than by being eaten.
        /// </summary>
        internal void ForfeitTreasureBonus()
        {
            IsTreasureBonusAvailable = false;
        }

        internal int Collect(ArchivalObjectDescriptor descriptor, HealthBody health, double foodHeal)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(health);
            int score = ScoreFor(descriptor.Kind, descriptor.Variant);
#pragma warning disable IDE0010 // Most runtime entity kinds intentionally have no collection side effect.
            switch (descriptor.Kind)
            {
                case EntityKind.Food:
                    health.Heal(foodHeal);
                    break;
                case EntityKind.Key:
                    _keyMask |= 1 << (descriptor.Variant - 1);
                    break;
                case EntityKind.Grenade:
                case EntityKind.GrenadeBox:
                    _ = AddAmmo(WeaponKind.Grenade, descriptor.Variant);
                    break;
                case EntityKind.Rocket:
                case EntityKind.RocketBox:
                    _ = AddAmmo(WeaponKind.Rocket, descriptor.Variant);
                    break;
                case EntityKind.MineBox:
                    _ = AddAmmo(WeaponKind.Mine, descriptor.Variant);
                    break;
                case EntityKind.BigBomb:
                    _ = AddAmmo(WeaponKind.BigBomb, descriptor.Variant);
                    break;
                case EntityKind.Ice:
                    IceRemaining += BonusDuration;
                    break;
                default:
                    break;
            }
#pragma warning restore IDE0010

            Score += score;
            return score;
        }

        internal void Advance(TimeSpan elapsed)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
            ColaRemaining = Reduce(ColaRemaining, elapsed);
            IceRemaining = Reduce(IceRemaining, elapsed);
        }

        internal int OpenDoor()
        {
            const int DoorScore = 100;
            Score += DoorScore;
            return DoorScore;
        }

        /// <summary>
        /// Makes a weapon current, the recovered <c>WeapCurrSet</c>, which refuses the weapon that
        /// is already current and any weapon without ammunition.
        /// </summary>
        /// <returns><see langword="true"/> when the current weapon changed.</returns>
        internal bool SelectWeapon(WeaponKind weapon)
        {
            if (weapon == CurrentWeapon || AmmoOf(weapon) <= 0)
            {
                return false;
            }

            CurrentWeapon = weapon;
            return true;
        }

        /// <summary>
        /// Selects the next stocked weapon after the current one, wrapping around, the recovered
        /// <c>WeapNext</c>. The current weapon is tried last, which selects nothing.
        /// </summary>
        /// <returns><see langword="true"/> when the current weapon changed.</returns>
        internal bool SelectNextWeapon()
        {
            int count = _ammo.Length;
            for (int step = 1; step <= count; step++)
            {
                WeaponKind candidate = (WeaponKind)(((int)CurrentWeapon + step) % count);
                if (AmmoOf(candidate) > 0)
                {
                    return SelectWeapon(candidate);
                }
            }

            return false;
        }

        /// <summary>
        /// Spends one round, the recovered <c>WeapDec</c>, which moves on to the next stocked
        /// weapon when the count reaches zero.
        /// </summary>
        /// <returns><see langword="true"/> when the current weapon changed.</returns>
        internal bool SpendAmmo(WeaponKind weapon)
        {
            int remaining = --_ammo[(int)weapon];
            return remaining == 0 && SelectNextWeapon();
        }

        /// <summary>
        /// Adds rounds, the recovered <c>WeapInc</c>, which selects the replenished weapon when the
        /// current one has none.
        /// </summary>
        /// <returns><see langword="true"/> when the current weapon changed.</returns>
        internal bool AddAmmo(WeaponKind weapon, int rounds)
        {
            if (rounds <= 0)
            {
                return false;
            }

            _ammo[(int)weapon] += rounds;
            return AmmoOf(CurrentWeapon) <= 0 && SelectWeapon(weapon);
        }

        internal void AddScore(int amount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(amount);
            Score += amount;
        }

        /// <summary>
        /// Turns the cola protection on, applying the recovered <c>_AEnd_BonCola</c>. Collection
        /// itself never does: the drink that follows it has to finish first.
        /// </summary>
        internal void StartCola()
        {
            ColaRemaining += BonusDuration;
        }

        internal void ClearIce()
        {
            IceRemaining = TimeSpan.Zero;
        }

        private static int ScoreFor(EntityKind kind, int variant)
        {
            return kind == EntityKind.Coin
                ? variant switch
                {
                    1 => 1,
                    2 => 5,
                    3 => 10,
                    _ => 0
                }
                : kind is EntityKind.Diamond or EntityKind.Food ? 50
                : kind == EntityKind.Key ? 100
                : kind is EntityKind.Grenade or EntityKind.Rocket ? 5
                : kind is EntityKind.GrenadeBox or EntityKind.RocketBox or EntityKind.MineBox ? 100
                : kind is EntityKind.BigBomb or EntityKind.Cola or EntityKind.Ice ? 250
                : 0;
        }

        private static TimeSpan Reduce(TimeSpan remaining, TimeSpan elapsed)
        {
            return elapsed >= remaining ? TimeSpan.Zero : remaining - elapsed;
        }
    }
}
