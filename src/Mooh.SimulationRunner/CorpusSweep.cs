using System;
using System.Collections.Generic;

using Mooh.Core.Content;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;

namespace Mooh.SimulationRunner
{
    /// <summary>
    /// Plays every level of a pack with a seeded random-input bot, so any simulation path that
    /// throws on real content shows up as a named level rather than in play.
    /// </summary>
    public static class CorpusSweep
    {
        private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan DecisionInterval = TimeSpan.FromMilliseconds(400);

        /// <summary>Plays each level of a pack for a fixed simulated duration.</summary>
        /// <param name="pack">The loaded pack.</param>
        /// <param name="duration">The simulated time to play each level for.</param>
        /// <param name="seed">The bot's random seed; each level derives its own from it.</param>
        /// <returns>One result per level, in pack order.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="pack"/> is <see langword="null"/>.</exception>
        public static IEnumerable<CorpusSweepResult> Run(LoadedLevelPack pack, TimeSpan duration, int seed)
        {
            ArgumentNullException.ThrowIfNull(pack);
            for (int index = 0; index < pack.Levels.Count; index++)
            {
                string name = pack.Manifest.Levels is { } entries && index < entries.Count
                    ? entries[index].Name ?? $"#{index}"
                    : $"#{index}";
                yield return Play(pack.Levels[index], name, duration, seed + index);
            }
        }

        private static CorpusSweepResult Play(LevelDocument document, string name, TimeSpan duration, int seed)
        {
            Random bot = new(seed);
            MaterializedLevel? level = null;
            int eventCount = 0;
            try
            {
                level = LevelMaterializer.Materialize(document);
                eventCount += level.Simulation.BeginLevel().Count;
                TimeSpan untilDecision = TimeSpan.Zero;
                for (TimeSpan elapsed = TimeSpan.Zero; elapsed < duration; elapsed += Step)
                {
                    if (level.Simulation.Outcome != LevelOutcome.InProgress)
                    {
                        break;
                    }

                    untilDecision -= Step;
                    if (untilDecision <= TimeSpan.Zero)
                    {
                        untilDecision = DecisionInterval;
                        level.Simulation.SetPlayerInput(Decide(bot));
                        if (bot.Next(8) == 0)
                        {
                            eventCount += level.Simulation.SelectWeapon((WeaponKind)bot.Next(4)).Count;
                        }
                    }

                    eventCount += level.Simulation.Advance(Step).Count;
                }

                return Result(name, level, eventCount, failure: null);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or ArgumentException
                or KeyNotFoundException
                or NullReferenceException
                or IndexOutOfRangeException
                or LevelMaterializationException)
            {
                return Result(name, level, eventCount, $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        private static PlayerInputState Decide(Random bot)
        {
            Direction? direction = bot.Next(6) switch
            {
                0 => Direction.Left,
                1 => Direction.Up,
                2 => Direction.Right,
                3 => Direction.Down,
                _ => null,
            };
            return new PlayerInputState(
                direction == Direction.Left,
                direction == Direction.Up,
                direction == Direction.Right,
                direction == Direction.Down,
                Fire: bot.Next(10) < 3);
        }

        private static CorpusSweepResult Result(
            string name,
            MaterializedLevel? level,
            int eventCount,
            string? failure)
        {
            return new CorpusSweepResult(
                name,
                level?.Simulation.Outcome ?? LevelOutcome.InProgress,
                level?.World.Player?.PlayerBody?.Progress.Score ?? 0,
                eventCount,
                failure);
        }
    }
}
