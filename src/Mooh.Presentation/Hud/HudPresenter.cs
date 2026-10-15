using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Mooh.Core.Combat;
using Mooh.Core.Content;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Core.Simulation;
using Mooh.Presentation.Text;

namespace Mooh.Presentation.Hud
{
    /// <summary>
    /// Keeps the screen panels and the help panel of one level in step with the simulation, and
    /// lists what to draw of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The panels are the recovered <c>PnlInit</c>, <c>WeapInit</c> and <c>BonInit</c> lists. A
    /// panel tied to a kind of object, through its <c>CMask2</c>, shows only when the level starts
    /// with such an object in it, as <c>PnlValReset</c>, <c>WeapValReset</c> and <c>BonReset</c>
    /// decide. A dim panel draws no value. Each panel's frame jerks on the native damped spring
    /// when its value is touched — the score on a gain, the health on harm, healing and the
    /// warnings, a monster count on a kill, a weapon on a pickup — while its value stays put.
    /// </para>
    /// <para>
    /// The glasses bonus panel is left out: no level places the glasses pickup, so
    /// <c>BonReset</c> always hides it. The bonus panels keep their recovered places, so the ice
    /// panel stays two rows up.
    /// </para>
    /// </remarks>
    public sealed class HudPresenter
    {
        /// <summary>The sprite page the panels draw from, <c>PgScrPanel</c>.</summary>
        public const string PageName = "SpriteScrPanel";

        /// <summary>The font every panel value and the help draw in, <c>PgPnlFontDig</c>.</summary>
        public const string FontName = "PanelDigFnt";

        /// <summary>The font of the level texts, <c>PgFontDigScr</c>.</summary>
        public const string LevelFontName = "PanelDigScr";

        /// <summary>How long a level text holds before it falls away, the 2000 ms every <c>TxtAdd</c> passes.</summary>
        private const double LevelTextHoldMilliseconds = 2000;

        /// <summary>The health below which the heartbeat warning jerks the panel, <c>CNT_GPrmHealthWrn</c>.</summary>
        private const double HealthWarning = 40;

        /// <summary>The clock time below which the warning ticks, <c>CNT_GPrmTimedWTime</c>.</summary>
        private const double ClockWarningMilliseconds = 30_000;

        /// <summary>The warning's tick, <c>CNT_GPrmTimedWTimeInt</c>.</summary>
        private const double ClockWarningTickMilliseconds = 750;

        /// <summary>The heartbeat warning's repeat, the <c>App.TimePast(ulGPrmHWrn) &gt; 1000</c> test.</summary>
        private const double HeartbeatMilliseconds = 1000;

        private static readonly WeaponKind[] Weapons = [WeaponKind.Grenade, WeaponKind.Rocket, WeaponKind.Mine, WeaponKind.BigBomb];

        private readonly HudPanel _health;
        private readonly HudPanel _score = new("PNrmScore", "PNrmScore", "PNrmScore", 36, 121, textRight: false);
        private readonly HudPanel _greenMonsters = new("PNrmMonGreen", "PNrmMonGreen", "PSmMonGreen", 765, 121, textRight: true);
        private readonly HudPanel _blueMonsters = new("PNrmMonBlue", "PNrmMonBlue", "PSmMonBlue", 765, 51, textRight: true);
        private readonly HudPanel[] _keys =
        [
            new("PNrmKeyBlue", "PNrmKeyBlue", "PSmKeyBlue", 480, 600, textRight: false),
            new("PNrmKeyRed", "PNrmKeyRed", "PSmKeyRed", 320, 600, textRight: false),
            new("PNrmKeyYellow", "PNrmKeyYellow", "PSmKeyYellow", 400, 600, textRight: false),
        ];

        private readonly HudPanel[] _weapons =
        [
            new("PNrmWeapGr", "PSelWeapGr", "PSmWeapGr", 36, 553, textRight: false),
            new("PNrmWeapRock", "PSelWeapRock", "PSmWeapRock", 36, 493, textRight: false),
            new("PNrmWeapMine", "PSelWeapMine", "PSmWeapMine", 36, 433, textRight: false),
            new("PNrmWeapBBomb", "PSelWeapBBomb", "PSmWeapBBomb", 36, 373, textRight: false),
        ];

        private readonly HudPanel _cola = new("PNrmBonCola", "PNrmBonCola", "PSmBonCola", 763, 553, textRight: true);
        private readonly HudPanel _ice = new("PNrmBonIce", "PNrmBonIce", "PSmBonIce", 763, 433, textRight: true);
        private readonly HashSet<HudPanel> _shown = [];
        private readonly HelpPanel _help = new();
        private readonly LevelTexts? _levelTexts;
        private readonly StringTable? _strings;
        private readonly Random _random;
        private readonly int[] _ammo = new int[4];
        private HashSet<EntityId> _greenMonsterIds = [];
        private HashSet<EntityId> _blueMonsterIds = [];
        private int? _lastScore;
        private bool _treasureAvailable;
        private bool _treasureEarned;
        private double? _lastHealth;
        private double _sinceHeartbeat = HeartbeatMilliseconds;
        private List<HudSprite> _sprites = [];
        private List<HudText> _texts = [];

        /// <summary>Starts the panels of a level, as the level's start resets them.</summary>
        /// <param name="world">The level's world at its start.</param>
        /// <param name="strings">The text table the help resolves its ids against, if any.</param>
        /// <param name="random">The source of the jerks' directions and the level texts' jiggles.</param>
        /// <param name="levelFont">The <see cref="LevelFontName"/> font the level texts are laid out in; without it none show.</param>
        /// <exception cref="ArgumentNullException"><paramref name="world"/> or <paramref name="random"/> is <see langword="null"/>.</exception>
        public HudPresenter(WorldGrid world, StringTable? strings, Random random, BitmapFont? levelFont = null)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(random);
            _strings = strings;
            _random = random;
            _levelTexts = levelFont is null ? null : new LevelTexts(levelFont);
            bool timed = world.Player?.HealthBody?.Clock is not null;
            _health = timed
                ? new HudPanel("PNrmTime", "PNrmTime", "PNrmTime", 36, 51, textRight: false)
                : new HudPanel("PNrmHealth", "PNrmHealth", "PNrmHealth", 36, 51, textRight: false);
            _ = _shown.Add(_health);
            _ = _shown.Add(_score);
            _ = _shown.Add(_weapons[0]);
            foreach (Entity entity in world.Entities)
            {
                if (ScreenPanelOf(entity) is HudPanel panel)
                {
                    _ = _shown.Add(panel);
                }
            }

            if (world.Player?.PlayerBody?.Progress is PlayerProgress progress)
            {
                _treasureAvailable = progress.IsTreasureBonusAvailable;
                _treasureEarned = progress.HasEarnedTreasureBonus;
                for (int index = 0; index < Weapons.Length; index++)
                {
                    _ammo[index] = progress.AmmoOf(Weapons[index]);
                }
            }
        }

        /// <summary>Gets the panel frames to draw, in draw order.</summary>
        public IReadOnlyList<HudSprite> Sprites => _sprites;

        /// <summary>Gets the lines of text to draw over the frames, in draw order.</summary>
        public IReadOnlyList<HudText> Texts => _texts;

        /// <summary>Drops in the level's title, <c>GameScrLvl</c>, as the level's play starts.</summary>
        /// <param name="number">The level's number, counting from 1.</param>
        public void ShowLevelTitle(int number)
        {
            ShowLevelText("GameScrLvl", number);
        }

        /// <summary>Follows the level through one frame.</summary>
        /// <param name="world">The simulated world.</param>
        /// <param name="events">The events the simulation reported for this frame.</param>
        /// <param name="score">The score the score panel shows, <c>HScoreScoreGet</c>.</param>
        /// <param name="seconds">The simulated time of this frame.</param>
        /// <exception cref="ArgumentNullException"><paramref name="world"/> or <paramref name="events"/> is <see langword="null"/>.</exception>
        public void Update(WorldGrid world, IReadOnlyList<SimulationEvent> events, int score, double seconds)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(events);
            double milliseconds = seconds * 1000;
            Entity? player = world.Player;
            HealthBody? health = player?.HealthBody;
            PlayerProgress? progress = player?.PlayerBody?.Progress;

            React(events, player, score);
            WarnHealth(health, milliseconds);
            FollowTreasure(progress);
            if (progress is not null)
            {
                for (int index = 0; index < Weapons.Length; index++)
                {
                    int ammo = progress.AmmoOf(Weapons[index]);
                    if (ammo > _ammo[index])
                    {
                        _weapons[index].Jerk(_random);
                    }

                    _ammo[index] = ammo;
                }
            }

            _greenMonsterIds = [.. world.Entities.Where(entity => entity.GreenMonsterBody is not null).Select(entity => entity.Id)];
            _blueMonsterIds = [.. world.Entities.Where(entity => entity.BlueMonsterBody is not null).Select(entity => entity.Id)];
            foreach (HudPanel panel in _shown)
            {
                panel.Spring.Advance(milliseconds);
            }

            _help.Advance(milliseconds);
            _levelTexts?.Advance(milliseconds, _random);
            List<HudSprite> sprites = [];
            List<HudText> texts = [];
            for (int index = 0; index < Weapons.Length; index++)
            {
                int ammo = _ammo[index];
                bool selected = progress?.CurrentWeapon == Weapons[index];
                Draw(sprites, texts, _weapons[index], ammo == 0 ? null : selected, ammo);
            }

            Draw(sprites, texts, _cola, Bonus(progress?.ColaRemaining));
            Draw(sprites, texts, _ice, Bonus(progress?.IceRemaining));
            Draw(sprites, texts, _greenMonsters, false, _greenMonsterIds.Count);
            Draw(sprites, texts, _blueMonsters, false, _blueMonsterIds.Count);
            Draw(sprites, texts, _health, false, HealthValue(health));
            Draw(sprites, texts, _score, false, score);
            for (int key = 1; key <= _keys.Length; key++)
            {
                Draw(sprites, texts, _keys[key - 1], progress?.HasKey(key) == true ? false : null, value: null);
            }

            _levelTexts?.Draw(texts);
            _help.Draw(texts);
            _sprites = sprites;
            _texts = texts;
        }

        private static (bool? Lit, int? Value) Bonus(TimeSpan? remaining)
        {
            // CBonInfo._RFunc: F2IN(EvDataGet(OEvOnTimeEnd) / 1000), rounded to nearest.
            return remaining is TimeSpan left && left > TimeSpan.Zero
                ? (false, (int)Math.Round(left.TotalMilliseconds / 1000))
                : (null, null);
        }

        /// <summary>
        /// The health panel's value: <c>PnlTimeGet</c>, the whole seconds left, or
        /// <c>PnlHealGet</c>, the health rounded up by <c>F2IU</c>.
        /// </summary>
        private static int HealthValue(HealthBody? health)
        {
            return health?.Clock is PlayClock clock
                ? (int)Math.Max(0, Math.Floor(clock.Remaining.TotalMilliseconds / 1000))
                : (int)Math.Ceiling(Math.Max(0, health?.Current ?? 0));
        }

        private void React(IReadOnlyList<SimulationEvent> events, Entity? player, int score)
        {
            if (_lastScore is int last && score > last)
            {
                _score.Jerk(_random);
            }

            _lastScore = score;
            foreach (SimulationEvent simulationEvent in events)
            {
                switch (simulationEvent)
                {
                    case DamageApplied damage when damage.EntityId == player?.Id:
                    case PickupCollected { Kind: EntityKind.Food }:
                        _health.Jerk(_random);
                        break;
                    case EntityDestroyed destroyed when _greenMonsterIds.Contains(destroyed.EntityId):
                        _greenMonsters.Jerk(_random);
                        break;
                    case EntityDestroyed destroyed when _blueMonsterIds.Contains(destroyed.EntityId):
                        _blueMonsters.Jerk(_random);
                        break;
                    case LevelOutcomeAnnounced announced:
                        ShowLevelText(announced.Outcome == LevelOutcome.Won
                            ? "GameScrWiner"
                            : announced.TimedOut ? "GameScrTimeOut" : "GameScrLooser");
                        break;
                    case HelpTextRequested help:
                        _help.Add(help.HelpTextId, _strings is not null && _strings.TryGetText(help.HelpTextId, out string text) ? text : null);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Announces the treasure bonus: <c>GameScrLostTreas</c> when <c>Game.ObjKill</c> forfeits
        /// it, <c>GameScrAllTreas</c> when the last treasure is eaten.
        /// </summary>
        private void FollowTreasure(PlayerProgress? progress)
        {
            if (progress is null)
            {
                return;
            }

            if (!_treasureEarned && progress.HasEarnedTreasureBonus)
            {
                ShowLevelText("GameScrAllTreas");
            }
            else if (_treasureAvailable && !progress.IsTreasureBonusAvailable && !progress.HasEarnedTreasureBonus)
            {
                ShowLevelText("GameScrLostTreas");
            }

            _treasureAvailable = progress.IsTreasureBonusAvailable;
            _treasureEarned = progress.HasEarnedTreasureBonus;
        }

        private void ShowLevelText(string id, int? number = null)
        {
            if (_levelTexts is null || _strings is null || !_strings.TryGetText(id, out string text))
            {
                return;
            }

            _levelTexts.Add(
                number is int value ? text.Replace("%d", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) : text,
                LevelTextHoldMilliseconds);
        }

        /// <summary>
        /// Jerks the health panel with the original's warnings: under 40 health, once a second
        /// while it drains; on a timed level, at every 750 ms tick of the last 30 seconds.
        /// </summary>
        private void WarnHealth(HealthBody? health, double milliseconds)
        {
            _sinceHeartbeat += milliseconds;
            if (health?.Clock is PlayClock clock)
            {
                double left = clock.Remaining.TotalMilliseconds;
                if (_lastHealth is double before
                    && left < ClockWarningMilliseconds
                    && Math.Floor(left / ClockWarningTickMilliseconds) != Math.Floor(before / ClockWarningTickMilliseconds))
                {
                    _health.Jerk(_random);
                }

                _lastHealth = left;
                return;
            }

            double current = health?.Current ?? 0;
            if (_lastHealth is double previous
                && current < previous
                && current is > 0 and < HealthWarning
                && _sinceHeartbeat > HeartbeatMilliseconds)
            {
                _sinceHeartbeat = 0;
                _health.Jerk(_random);
            }

            _lastHealth = current;
        }

        private void Draw(List<HudSprite> sprites, List<HudText> texts, HudPanel panel, (bool? Lit, int? Value) state)
        {
            Draw(sprites, texts, panel, state.Lit, state.Value);
        }

        /// <summary>Lists a shown panel: dim when <paramref name="lit"/> is <see langword="null"/>, without its value.</summary>
        private void Draw(List<HudSprite> sprites, List<HudText> texts, HudPanel panel, bool? lit, int? value)
        {
            if (!_shown.Contains(panel))
            {
                return;
            }

            string frame = lit switch
            {
                null => panel.Dim,
                true => panel.Lit,
                false => panel.Normal,
            };
            sprites.Add(new HudSprite(
                frame,
                (int)Math.Floor(panel.X + panel.Spring.X),
                (int)Math.Floor(panel.Y + panel.Spring.Y)));
            if (lit is not null && value is int shown)
            {
                (int x, int y) = panel.TextPoint;
                texts.Add(new HudText(
                    shown.ToString(CultureInfo.InvariantCulture),
                    x,
                    y,
                    panel.TextRight ? TextAlignment.Right : TextAlignment.Left));
            }
        }

        /// <summary>The panel an object at the level's start shows, through its <c>ulCMask2</c>.</summary>
        private HudPanel? ScreenPanelOf(Entity entity)
        {
            if (entity.GreenMonsterBody is not null)
            {
                return _greenMonsters;
            }

            if (entity.BlueMonsterBody is not null)
            {
                return _blueMonsters;
            }

            if (entity.ArchivalIdentity is not ArchivalObjectIdentity identity)
            {
                return null;
            }

#pragma warning disable IDE0010 // Most kinds tie to no panel.
            switch (identity.Kind)
            {
                case EntityKind.Rocket:
                case EntityKind.RocketBox:
                    return _weapons[1];
                case EntityKind.MineBox:
                    return _weapons[2];
                case EntityKind.BigBomb:
                    return _weapons[3];
                case EntityKind.Cola:
                    return _cola;
                case EntityKind.Ice:
                    return _ice;
                case EntityKind.Key when identity.Variant is >= 1 and <= 3:
                    return _keys[identity.Variant - 1];
                default:
                    return null;
            }
#pragma warning restore IDE0010
        }
    }
}
