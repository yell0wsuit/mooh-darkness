using System;
using System.Collections.Generic;

using Mooh.Core.Combat;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Scene;

namespace Mooh.Presentation.Audio
{
    /// <summary>
    /// The sounds the game module plays itself rather than an object's handlers: the outcome
    /// and clock sounds of <c>__GStatePlay</c>, the bonuses of <c>CBonInfo</c>, <c>WeapInc</c>'s
    /// pickup, the all-treasure bonus and the low-health heartbeat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bonus sounds follow the player's timed bonuses: <c>GMod.BonOn</c> plays the bonus's
    /// <c>SndOn</c> at the player as it starts, the cola then plays <c>SndBonColaAmb</c> every 300 to
    /// 350 ms while it lasts, and a bonus that runs out plays its <c>SndOff</c> (the ice's is its
    /// <c>SndBonIceOn</c> again); a hit the cola soaks up plays <c>SndBonColaWork</c> and
    /// <c>SndBonColaFizz</c>. The heartbeat, <c>SndPlHeart1</c>, beats at most once a second
    /// while health drains below 40.
    /// </para>
    /// <para>
    /// The ambient loops of <c>AmbSndList</c> are left out on purpose: every alias they name
    /// (<c>BioAmb01</c>, <c>FireAmb01</c> and the rest) is missing from the shipped
    /// <c>Sound.json</c>, so <c>ObjSoundPlay</c> never played them. The timed warning ticks are
    /// silent for the same reason: <c>SndPlay(SndClockWrn[x % len(SndClockWrn)])</c> indexes the
    /// string <c>ClockWarning</c> and names a single letter.
    /// </para>
    /// </remarks>
    /// <param name="library">The recovered sound names.</param>
    /// <param name="random">The source of the cola ambient's timing.</param>
    internal sealed class GameSounds(AnimationLibrary library, Random random)
    {
        private const double HealthWarning = 40;
        private const double HeartbeatMilliseconds = 1000;

        private static readonly WeaponKind[] Weapons = [WeaponKind.Grenade, WeaponKind.Rocket, WeaponKind.Mine, WeaponKind.BigBomb];

        private readonly int[] _ammo = new int[4];
        private bool _started;
        private readonly HashSet<EntityId> _rockets = [];
        private bool _drinkingCola;
        private double _physicsMilliseconds;
        private double? _teleportLastPulse;
        private double? _shelterNextBite;
        private readonly List<double> _shelterChews = [];
        private TimeSpan _cola;
        private TimeSpan _ice;
        private double? _colaAmbientMilliseconds;
        private bool _treasureEarned;
        private bool _clockOut;
        private double? _health;
        private double _sinceHeartbeat = HeartbeatMilliseconds;

        public IEnumerable<SoundCue> For(WorldGrid world, IReadOnlyList<SimulationEvent> events, double seconds, double physicsSeconds)
        {
            double milliseconds = seconds * 1000;
            _physicsMilliseconds += physicsSeconds * 1000;
            List<SoundCue> cues = [];
            Entity? player = world.Player;
            (double x, double y) = player is null ? (0, 0) : DrawnPosition.Of(player);
            foreach (SimulationEvent item in events)
            {
                switch (item)
                {
                    case RocketFired rocket:
                        _ = _rockets.Add(rocket.RocketEntityId);
                        cues.Add(Placed("SndWeapRockRot", rocket.Position.X, rocket.Position.Y));
                        break;
                    case CellEntered entered when _rockets.Contains(entered.EntityId):
                        cues.Add(Placed("SndWeapRockRot", entered.Position.X, entered.Position.Y));
                        break;
                    case EntityDestroyed destroyed:
                        _ = _rockets.Remove(destroyed.EntityId);
                        break;
                    case PlayerTunnelReversed reversed:
                        cues.Add(Placed("SndTonFlip", reversed.Position.X, reversed.Position.Y));
                        break;
                    case PlayerTeleportStarted:
                        _teleportLastPulse = _physicsMilliseconds;
                        break;
                    case PlayerTeleportArrived:
                        _teleportLastPulse = null;
                        break;
                    case PlayerColaDrinkStarted:
                        _drinkingCola = true;
                        break;
                    default:
                        break;
                }
            }

            foreach (LevelOutcomeAnnounced announced in System.Linq.Enumerable.OfType<LevelOutcomeAnnounced>(events))
            {
                if (announced.Outcome == LevelOutcome.Won || announced.TimedOut)
                {
                    cues.Add(Centered("SndPlFinalOk", x, y));
                }
            }

            if (player?.PlayerBody?.Progress is not PlayerProgress progress || player.HealthBody is not HealthBody health)
            {
                return cues;
            }

            TravelAndShelter(player, x, y, cues);

            if (!_started)
            {
                _started = true;
                Remember(progress, health);
                return cues;
            }

            if (progress.ColaRemaining > TimeSpan.Zero
                && System.Linq.Enumerable.Any(events, item => item is DamageIgnored ignored && ignored.EntityId == player.Id))
            {
                // OnHelthLost's cola branch: the bonus soaks up the hit.
                cues.Add(Placed("SndBonColaWork", x, y));
                cues.Add(Placed("SndBonColaFizz", x, y));
            }

            if (_drinkingCola && progress.ColaRemaining > _cola)
            {
                cues.Add(Placed("SndBonColaFizz", x, y));
            }

            if (player.PlayerBody.Action?.Kind != PlayerActionKind.ColaDrink)
            {
                _drinkingCola = false;
            }

            Bonus(progress.ColaRemaining, ref _cola, "SndBonColaOn", "SndBonColaOff", milliseconds, x, y, cues);
            Bonus(progress.IceRemaining, ref _ice, "SndBonIceOn", "SndBonIceOn", milliseconds, x, y, cues);
            ColaAmbient(progress.ColaRemaining, milliseconds, x, y, cues);
            for (int index = 0; index < Weapons.Length; index++)
            {
                int ammo = progress.AmmoOf(Weapons[index]);
                if (ammo > _ammo[index])
                {
                    cues.Add(Placed("SndWeapGet", x, y));
                }
            }

            if (!_treasureEarned && progress.HasEarnedTreasureBonus)
            {
                cues.Add(Centered("SndCoinBon", x, y));
            }

            if (!_clockOut && health.Clock?.HasRunOut == true)
            {
                cues.Add(Centered("SndClockEnd", x, y));
            }

            _sinceHeartbeat += milliseconds;
            if (health.Clock is null
                && _health is double before
                && health.Current < before
                && health.Current is > 0 and < HealthWarning
                && _sinceHeartbeat > HeartbeatMilliseconds)
            {
                _sinceHeartbeat = 0;
                cues.Add(Centered("SndPlHeart1", x, y));
            }

            Remember(progress, health);
            return cues;
        }

        private void TravelAndShelter(Entity player, double x, double y, List<SoundCue> cues)
        {
            if (player.PlayerBody?.Action?.Kind == PlayerActionKind.TeleportFlight
                && _teleportLastPulse is double last && _physicsMilliseconds - last > 250)
            {
                cues.Add(Placed("SndTPortMove", x, y));
                _teleportLastPulse = _physicsMilliseconds;
            }

            if (player.PlayerBody?.Action is not { Kind: PlayerActionKind.ShelterHidden } hidden)
            {
                _shelterNextBite = null;
                _shelterChews.Clear();
                return;
            }

            double elapsed = 3000 - (hidden.RemainingSeconds * 1000);
            _shelterNextBite ??= 750;
            while (_shelterNextBite.Value < 3000 && elapsed >= _shelterNextBite.Value)
            {
                cues.Add(Placed("SndEatEat", x, y));
                _shelterChews.Add(_shelterNextBite.Value + random.Next(200, 250));
                _shelterNextBite += random.Next(750, 850);
            }

            for (int index = _shelterChews.Count - 1; index >= 0; index--)
            {
                if (elapsed >= _shelterChews[index])
                {
                    cues.Add(Placed("SndEatEat2", x, y));
                    _shelterChews.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Plays a bonus's <c>SndOn</c> when it starts or is extended, and its <c>SndOff</c> when it
        /// runs out within the frame rather than being cut short.
        /// </summary>
        private void Bonus(TimeSpan now, ref TimeSpan before, string on, string off, double milliseconds, double x, double y, List<SoundCue> cues)
        {
            if (now > before)
            {
                cues.Add(Placed(on, x, y));
            }
            else if (now == TimeSpan.Zero && before > TimeSpan.Zero && before.TotalMilliseconds <= milliseconds + 1)
            {
                cues.Add(Placed(off, x, y));
            }

            before = now;
        }

        /// <summary>The cola's <c>_AmbSnd</c>, rescheduled every <c>Range(300, 350)</c> ms.</summary>
        private void ColaAmbient(TimeSpan cola, double milliseconds, double x, double y, List<SoundCue> cues)
        {
            if (cola <= TimeSpan.Zero)
            {
                _colaAmbientMilliseconds = null;
                return;
            }

            if (_colaAmbientMilliseconds is not double left)
            {
                _colaAmbientMilliseconds = random.Next(300, 350);
                return;
            }

            left -= milliseconds;
            if (left <= 0)
            {
                cues.Add(Placed("SndBonColaAmb", x, y));
                left += random.Next(300, 350);
            }

            _colaAmbientMilliseconds = left;
        }

        private void Remember(PlayerProgress progress, HealthBody health)
        {
            for (int index = 0; index < Weapons.Length; index++)
            {
                _ammo[index] = progress.AmmoOf(Weapons[index]);
            }

            _cola = progress.ColaRemaining;
            _ice = progress.IceRemaining;
            _treasureEarned = progress.HasEarnedTreasureBonus;
            _clockOut = health.Clock?.HasRunOut == true;
            _health = health.Clock is null ? health.Current : null;
        }

        private SoundCue Placed(string name, double x, double y)
        {
            return new SoundCue(library.Sound(name), x, y);
        }

        private SoundCue Centered(string name, double x, double y)
        {
            return new SoundCue(library.Sound(name), x, y, Centered: true);
        }
    }
}
