using System;

using Mooh.Core.Combat;
using Mooh.Core.Levels;

namespace Mooh.Core.Player
{
    /// <summary>Stores recovered score, treasure, inventory, key, and timed-bonus state.</summary>
    public sealed class PlayerProgress
    {
        private static readonly TimeSpan BonusDuration = TimeSpan.FromSeconds(45);
        private int _keyMask;
        private bool _treasureBonusAwarded;

        /// <summary>Gets the level score earned by collection and progression.</summary>
        public int Score { get; private set; }

        /// <summary>Gets the number of collected coins and diamonds.</summary>
        public int TreasureCollected { get; private set; }

        /// <summary>Gets the number of coins and diamonds initially present in the level.</summary>
        public int TotalTreasure { get; private set; }

        /// <summary>Gets whether every initially placed treasure has been collected.</summary>
        public bool HasAllTreasure => TotalTreasure > 0 && TreasureCollected >= TotalTreasure;

        /// <summary>Gets the available grenade count, including the recovered five starting grenades.</summary>
        public int Grenades { get; private set; } = 5;

        /// <summary>Gets the available rocket count.</summary>
        public int Rockets { get; private set; }

        /// <summary>Gets the available mine count.</summary>
        public int Mines { get; private set; }

        /// <summary>Gets the available big-bomb count.</summary>
        public int BigBombs { get; private set; }

        /// <summary>Gets the remaining cola protection duration.</summary>
        public TimeSpan ColaRemaining { get; private set; }

        /// <summary>Gets the remaining monster-freezing ice duration.</summary>
        public TimeSpan IceRemaining { get; private set; }

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

        internal void InitializeTreasure(int totalTreasure)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(totalTreasure);
            TotalTreasure = totalTreasure;
        }

        internal int Collect(ArchivalObjectDescriptor descriptor, HealthBody health, double foodHeal)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(health);
            int score = ScoreFor(descriptor.Kind, descriptor.Variant);
#pragma warning disable IDE0010 // Most runtime entity kinds intentionally have no collection side effect.
            switch (descriptor.Kind)
            {
                case EntityKind.Coin:
                case EntityKind.Diamond:
                    TreasureCollected++;
                    if (HasAllTreasure && !_treasureBonusAwarded)
                    {
                        score += 5000;
                        _treasureBonusAwarded = true;
                    }

                    break;
                case EntityKind.Food:
                    health.Heal(foodHeal);
                    break;
                case EntityKind.Key:
                    _keyMask |= 1 << (descriptor.Variant - 1);
                    break;
                case EntityKind.Grenade:
                case EntityKind.GrenadeBox:
                    Grenades += descriptor.Variant;
                    break;
                case EntityKind.Rocket:
                case EntityKind.RocketBox:
                    Rockets += descriptor.Variant;
                    break;
                case EntityKind.MineBox:
                    Mines += descriptor.Variant;
                    break;
                case EntityKind.BigBomb:
                    BigBombs += descriptor.Variant;
                    break;
                case EntityKind.Cola:
                    ColaRemaining = BonusDuration;
                    break;
                case EntityKind.Ice:
                    IceRemaining = BonusDuration;
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

        internal void AddScore(int amount)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(amount);
            Score += amount;
        }

        internal void ClearIce()
        {
            IceRemaining = TimeSpan.Zero;
        }

        private static int ScoreFor(EntityKind kind, int variant)
        {
            return kind == EntityKind.Coin
                ? variant switch { 1 => 1, 2 => 5, 3 => 10, _ => 0 }
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
