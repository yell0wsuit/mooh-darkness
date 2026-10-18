using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// The floating scores and lines of <c>GameScoreAdd</c>, the overlay texts of
    /// <c>PartsOvr.AddText</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A score floats in <c>PanelDigFnt</c> 20 px above the object and its line in
    /// <c>PanelDigTextFnt</c> (<c>PanelDigTextRedFnt</c> for a hurt) 20 px below, drifting at
    /// 0.025 to 0.05 px/ms in a direction up to 2 radians either side of up, the line at three
    /// quarters of that, for <c>AddText</c>'s 3000 ms. Eating floats the type's
    /// <c>ulEatScore</c> and <c>GetTxt</c>, a kill its <c>ulKillScore</c> and <c>KillTxt</c>, a hurt
    /// its <c>FObjHealLost</c> or <c>FObjTimeLost</c>, and the all-treasure bonus its 5000.
    /// </para>
    /// <para>
    /// The native text render of <c>Parts2D</c> did not survive decompilation, so the centering
    /// and the fade over the call's 640 are inferred. A food's line names the health or time the
    /// player actually gained, which is <c>Compl.HEat</c> unless full health capped it.
    /// </para>
    /// </remarks>
    internal sealed class FloatingTexts(AnimationLibrary library, StringTable? strings, Random random)
    {
        private const string ScoreFont = "PanelDigFnt";
        private const string LineFont = "PanelDigTextFnt";
        private const string RedLineFont = "PanelDigTextRedFnt";
        private const double LifeMilliseconds = 3000;
        private const double FadeMilliseconds = 640;
        private const int TreasureBonus = 5000;

        private readonly List<Floating> _texts = [];
        private bool? _treasureOpen;
        private bool _treasureEarned;
        private double? _vitality;

        /// <summary>Gets the texts to draw, in order.</summary>
        public IEnumerable<SceneText> Texts => _texts.Select(text => new SceneText(
            text.Font,
            text.Text,
            text.X,
            text.Y,
            Math.Clamp((LifeMilliseconds - text.Age) / FadeMilliseconds, 0, 1)));

        public void Update(WorldGrid world, IReadOnlyList<SimulationEvent> events, IReadOnlyDictionary<EntityId, Sighting> seen, double seconds)
        {
            double milliseconds = seconds * 1000;
            foreach (Floating text in _texts)
            {
                text.Advance(milliseconds);
            }

            _ = _texts.RemoveAll(text => text.Age >= LifeMilliseconds);
            Entity? player = world.Player;
            PlayerProgress? progress = player?.PlayerBody?.Progress;
            HealthBody? health = player?.HealthBody;
            bool timed = health?.Clock is not null;
            double? vitality = health?.Clock is PlayClock clock ? clock.Remaining.TotalSeconds : health?.Current;
            bool treasureOpen = _treasureOpen ?? progress?.IsTreasureBonusAvailable ?? false;
            int gain = vitality is double now && _vitality is double before ? (int)Math.Round(Math.Max(0, now - before)) : 0;

            HashSet<EntityId> eaten = [];
            foreach (SimulationEvent item in events)
            {
                EntityId? eatenId = item switch
                {
                    PlayerAte ate => ate.EatenEntityId,
                    PickupCollected collected => collected.PickupEntityId,
                    _ => null,
                };
                if (eatenId is EntityId id && eaten.Add(id) && seen.TryGetValue(id, out Sighting meal) && LookOf(meal) is ObjectLook food)
                {
                    Add(meal.X, meal.Y, food.EatScore, food.EatLine, red: false, timed, treasureOpen, gain);
                }
                else if (item is EntityDestroyed destroyed && !eaten.Contains(destroyed.EntityId)
                    && !events.Any(other => (other is PlayerAte ate && ate.EatenEntityId == destroyed.EntityId)
                        || (other is PickupCollected collected && collected.PickupEntityId == destroyed.EntityId))
                    && seen.TryGetValue(destroyed.EntityId, out Sighting victim) && LookOf(victim) is ObjectLook killed)
                {
                    Add(victim.X, victim.Y, killed.KillScore, killed.KillLine, red: false, timed, treasureOpen, gain);
                }
                else if (item is DamageApplied { Amount: > 0 } damage && seen.TryGetValue(damage.EntityId, out Sighting hurt) && hurt.IsPlayer)
                {
                    Add(hurt.X, hurt.Y, 0, new ScoreLine(timed ? "FObjTimeLost" : "FObjHealLost", Argument: (int)damage.Amount), red: true, timed, treasureOpen, gain);
                }
            }

            if (progress is not null && !_treasureEarned && progress.HasEarnedTreasureBonus && player is not null)
            {
                (double x, double y) = DrawnPosition.Of(player);
                Add(x, y, TreasureBonus, null, red: false, timed, treasureOpen, gain);
            }

            _treasureOpen = progress?.IsTreasureBonusAvailable ?? false;
            _treasureEarned = progress?.HasEarnedTreasureBonus ?? false;
            _vitality = vitality;
        }

        /// <summary><c>GameScoreAdd(ulScore, M, Txt, bTxtRed)</c> at an object.</summary>
        private void Add(double x, double y, int score, ScoreLine? line, bool red, bool timed, bool treasureOpen, int gain)
        {
            double speed = 0.025 + (random.NextDouble() * 0.025);
            double angle = -2 + (random.NextDouble() * 4);
            (double sine, double cosine) = Math.SinCos(angle);
            (double speedX, double speedY) = (speed * sine, -speed * cosine);
            if (score > 0)
            {
                _texts.Add(new Floating(ScoreFont, score.ToString(CultureInfo.InvariantCulture), x, y - (20 / SceneCamera.CellHeight), speedX, speedY));
            }

            if (line is null || (line.WhileTreasure && !treasureOpen) || TextOf(line, timed, gain) is not string text)
            {
                return;
            }

            _texts.Add(new Floating(red ? RedLineFont : LineFont, text, x, y + (20 / SceneCamera.CellHeight), speedX * 0.75, speedY * 0.75));
        }

        private string? TextOf(ScoreLine line, bool timed, int gain)
        {
            string id = timed && line.TimedId is string timedId ? timedId : line.Id;
            if (strings is null || !strings.TryGetText(id, out string text))
            {
                return null;
            }

            int? number = line.FormatsGain ? gain : line.Argument;
            return number is int value ? text.Replace("%d", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) : text;
        }

        private ObjectLook? LookOf(Sighting at)
        {
            return at.Type is string type && library.TryGetLook(type, out ObjectLook look) ? look : null;
        }

        /// <summary>One drifting text; positions in cells, speeds given in pixels per millisecond.</summary>
        private sealed class Floating(string font, string text, double x, double y, double speedX, double speedY)
        {
            public string Font { get; } = font;

            public string Text { get; } = text;

            public double X { get; private set; } = x;

            public double Y { get; private set; } = y;

            public double Age
            {
                get; private set;
            }

            public void Advance(double milliseconds)
            {
                Age += milliseconds;
                X += speedX * milliseconds / SceneCamera.CellWidth;
                Y += speedY * milliseconds / SceneCamera.CellHeight;
            }
        }
    }
}
