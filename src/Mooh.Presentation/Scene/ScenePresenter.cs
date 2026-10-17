using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Content;
using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;
using Mooh.Presentation.Audio;
using Mooh.Presentation.Sprites;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// Keeps one animator per simulated entity and turns the world into an ordered draw list.
    /// </summary>
    /// <remarks>
    /// The presenter only reads the simulation. Each update advances every animator by the frame's
    /// simulated time, then lists the frames to draw in the native <c>ListRenderSort</c> order.
    /// </remarks>
    public sealed class ScenePresenter
    {
        /// <summary>The start door's registration, <c>CNT_ONameDoor</c>.</summary>
        private const string StartDoorType = "ODoor";

        /// <summary>
        /// The registration the simulation's thrown grenade shares. The original's own
        /// <c>CNT_ONameGrenade</c>, <c>WBombInt</c>, has the same <c>GameTypeBomb</c> states.
        /// </summary>
        private const string ThrownGrenadeType = "WBomb";

        /// <summary>The laid mine's registration, <c>CNT_ONameMine</c>, which draws on pass 1.</summary>
        private const string MineType = "WMine";

        /// <summary>The message table's registration, which draws on pass 1.</summary>
        private const string MessageTableType = "OTable";

        private readonly Dictionary<EntityId, EntityAnimator> _animators = [];
        private readonly EffectSystem _effects;
        private readonly EventSounds _eventSounds;
        private readonly ScreenShake _shake = new();
        private readonly ParticleSystem _particles;
        private readonly FloatingTexts? _floating;
        private readonly GameSounds _gameSounds;
        private readonly WaterDrops _waterDrops;
        private List<SceneText> _texts = [];
        private Dictionary<EntityId, Sighting> _seen = [];
        private double _clockMilliseconds;
        private readonly AnimationLibrary _library;
        private readonly Random _random;
        private Dictionary<EntityId, Sighting> _currentSeen = [];
        private readonly Dictionary<EntityId, Sighting> _sighted = [];
        private readonly List<EntityId> _removedAnimators = [];
        private readonly List<SceneSprite> _spriteBuffer = [];
        private readonly List<SpriteOrder> _spriteOrder = [];
        private readonly List<(double X, double Y)> _steps = [];
        private readonly List<(double X, double Y, Direction Direction)> _trails = [];
        private List<SoundCue> _sounds = [];

        /// <summary>Initializes the presenter.</summary>
        /// <param name="library">The recovered animation tables.</param>
        /// <param name="random">The source of the original's presentation-only random choices.</param>
        /// <param name="strings">The recovered text table the floating lines read, if loaded.</param>
        /// <param name="masks">The sprites' solid pixels, which the pickup sparks need, if loaded.</param>
        /// <param name="showFloatingText">Whether to emit floating score and status labels; disabled by default in this port.</param>
        /// <exception cref="ArgumentNullException"><paramref name="library"/> or <paramref name="random"/> is <see langword="null"/>.</exception>
        public ScenePresenter(AnimationLibrary library, Random random, StringTable? strings = null, ISpriteMask? masks = null, bool showFloatingText = false)
        {
            ArgumentNullException.ThrowIfNull(library);
            ArgumentNullException.ThrowIfNull(random);
            _library = library;
            _random = random;
            _effects = new EffectSystem(library, random);
            _particles = new ParticleSystem(library, random, masks);
            _floating = showFloatingText ? new FloatingTexts(library, strings, random) : null;
            _gameSounds = new GameSounds(library, random);
            _waterDrops = new WaterDrops(library, random);
            _eventSounds = new EventSounds(library, random);
        }

        /// <summary>Gets the sounds cued this update, in order.</summary>
        public IReadOnlyList<SoundCue> Sounds => _sounds;

        /// <summary>Gets how far the view is jerked off its place, in screen pixels.</summary>
        public (double X, double Y) Shake => (_shake.X, _shake.Y);

        /// <summary>Gets the floating scores and lines, drawn over the world, in order.</summary>
        public IReadOnlyList<SceneText> Texts => _texts;

        /// <summary>Gets the frames to draw, in draw order.</summary>
        public IReadOnlyList<SceneSprite> Sprites
        {
            get; private set;
        } = [];

        /// <summary>Gets or sets the optional, purely visual terrain outside the level.</summary>
        public ExteriorScenery? Scenery
        {
            get; set;
        }

        /// <summary>Gets an entity's animator.</summary>
        /// <param name="id">The entity.</param>
        /// <returns>The animator, or <see langword="null"/> when the entity has none.</returns>
        public EntityAnimator? AnimatorOf(EntityId id)
        {
            return _animators.GetValueOrDefault(id);
        }

        /// <summary>Follows the world through one frame of simulation.</summary>
        /// <param name="world">The simulated world.</param>
        /// <param name="events">The events the simulation reported for this frame.</param>
        /// <param name="seconds">The simulated time of this frame.</param>
        /// <param name="spin">The world rotation in flight, which turns every drawn position.</param>
        /// <param name="build">The level's fly-in, which hides and moves what it has not landed yet.</param>
        /// <param name="bonusSeconds">The elapsed real time for bonuses and clock-based layers when physics and object animation are paused.</param>
        /// <param name="camera">The visible region to fill with exterior scenery.</param>
        /// <exception cref="ArgumentNullException"><paramref name="world"/> or <paramref name="events"/> is <see langword="null"/>.</exception>
        public void Update(WorldGrid world, IReadOnlyList<SimulationEvent> events, double seconds, WorldSpin? spin = null, LevelBuild? build = null, double? bonusSeconds = null, SceneCamera? camera = null)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(events);

            build?.IncludeView(camera, Scenery);

            _clockMilliseconds += (bonusSeconds ?? seconds) * 1000;
            Dictionary<EntityId, Sighting> seen = _currentSeen;
            seen.Clear();
            List<SceneSprite> sprites = _spriteBuffer;
            sprites.Clear();
            if (Scenery is ExteriorScenery scenery)
            {
                foreach (WorldRotationCompleted turn in events.OfType<WorldRotationCompleted>())
                {
                    scenery.Rotate(turn.Direction);
                    build?.ForgetSceneryFlights();
                }

                if (camera is not null)
                {
                    IReadOnlyList<SceneSprite> exterior = scenery.Layout(camera, spin);
                    if (build is null)
                    {
                        sprites.AddRange(exterior);
                    }
                    else
                    {
                        foreach (SceneSprite sprite in exterior)
                        {
                            if (build.PresentScenery(sprite) is SceneSprite shown)
                            {
                                sprites.Add(shown);
                            }
                        }
                    }
                }
            }
            List<SoundCue> sounds = [];
            List<(double X, double Y)> steps = _steps;
            steps.Clear();
            List<(double X, double Y, Direction Direction)> trails = _trails;
            trails.Clear();
            foreach (Entity entity in world.Entities)
            {
                (double seenX, double seenY) = DrawnPosition.Of(entity);
                seen[entity.Id] = new Sighting(TypeOf(entity), seenX, seenY, entity.PlayerBody is not null, entity.BigBombBody is not null, entity.IsFrozenMonster);
                if (build?.IsDone == false && (entity.PlayerBody is not null || entity.DoorBody is not null))
                {
                    // Their GameInit runs after construction, so neither animation nor its
                    // initial sound may start while the player and entry door are hidden.
                    continue;
                }

                if (!_animators.TryGetValue(entity.Id, out EntityAnimator? animator))
                {
                    animator = Create(entity);
                    if (animator is null)
                    {
                        continue;
                    }

                    _animators[entity.Id] = animator;
                }

                animator.Update(world, entity, events, seconds);
                bool hidden = build?.Hides(entity.Id) == true;
                for (int step = animator.TakeSteps(); step > 0; step--)
                {
                    if (!hidden)
                    {
                        steps.Add(DrawnPosition.Of(entity));
                    }
                }

                if (animator is ObjectAnimator obj && entity.FlyingArrowBody is { } arrow)
                {
                    for (int burst = obj.TakeFlightTrails(); burst > 0; burst--)
                    {
                        if (!hidden)
                        {
                            (double x, double y) = DrawnPosition.Of(entity);
                            trails.Add((x, y, arrow.Direction));
                        }
                    }
                }

                foreach (string alias in animator.TakeSounds())
                {
                    if (!hidden)
                    {
                        (double soundX, double soundY) = build?.PositionOf(entity) ?? DrawnPosition.Of(entity);
                        sounds.Add(new SoundCue(alias, soundX, soundY));
                    }
                }

                if (hidden)
                {
                    continue;
                }

                if (animator.Frame is string frame)
                {
                    (double x, double y) = build?.PositionOf(entity) ?? DrawnPosition.Of(entity);
                    (double dx, double dy) = animator.Offset;
                    int pass = build?.IsFlying(entity.Id) == true ? 1 : animator.Pass;
                    foreach ((string glowPage, string glowFrame) in animator.Underlays(_clockMilliseconds))
                    {
                        sprites.Add(new SceneSprite(entity.Id, glowPage, glowFrame, x + dx, y + dy, pass, Additive: true));
                    }

                    if (animator.Shadow is (string shadowPage, string shadowFrame) && CastsShadow(world, x, y))
                    {
                        sprites.Add(new SceneSprite(entity.Id, shadowPage, shadowFrame, x + dx, y + dy, pass));
                    }

                    if (animator.BodyUnderlay is { } layer)
                    {
                        sprites.Add(new SceneSprite(entity.Id, layer.Page, layer.Frame, x + dx, y + dy, pass, layer.Additive));
                    }

                    sprites.Add(new SceneSprite(entity.Id, animator.Page, frame, x + dx, y + dy, pass));
                    foreach ((string overlayPage, string overlayFrame) in animator.Overlays(_clockMilliseconds))
                    {
                        sprites.Add(new SceneSprite(entity.Id, overlayPage, overlayFrame, x + dx, y + dy, pass, Additive: true));
                    }
                }
            }

            // An event's entity may be gone by now; it sounds where it was last drawn.
            Dictionary<EntityId, Sighting> sighted = _sighted;
            sighted.Clear();
            foreach ((EntityId id, Sighting sighting) in _seen)
            {
                sighted.Add(id, sighting);
            }
            foreach ((EntityId id, Sighting sighting) in seen)
            {
                sighted[id] = sighting;
            }

            foreach (SmallBeetleSpawned spawned in events.OfType<SmallBeetleSpawned>())
            {
                // A beetle can be collected before the frame ever draws it.
                _ = sighted.TryAdd(spawned.SpawnedEntityId, new Sighting(
                    spawned.Color == SmallBeetleColor.Green ? "OSmGr" : "OSmBlue",
                    spawned.Position.X, spawned.Position.Y, false, false));
            }

            foreach (KeyDoorOpened opened in events.OfType<KeyDoorOpened>())
            {
                if (opened.Position is GridPosition position)
                {
                    _ = sighted.TryAdd(opened.DoorEntityId,
                        new Sighting($"OKDDoor{opened.KeyNumber}", position.X, position.Y, false, false));
                }
            }

            sounds.AddRange(_eventSounds.For(events, sighted, seconds));
            sounds.AddRange(_gameSounds.For(world, events, bonusSeconds ?? seconds, seconds));
            if (build is null || build.IsDone)
            {
                // GameWaterSpawn starts with play, after the fly-in.
                sounds.AddRange(_waterDrops.Update(world, seconds));
            }

            if (build is not null)
            {
                sounds.AddRange(build.Sounds);
            }

            _shake.Update(world, events, sighted, seconds, _random);
            _currentSeen = _seen;
            _seen = seen;
            _sounds = sounds;
            IReadOnlyList<GridPosition> blasts = _effects.Update(world, events, seconds);
            sprites.AddRange(_effects.Sprites);
            sprites.AddRange(_waterDrops.Sprites);
            if (build is null || build.IsDone)
            {
                // The pickup timers are installed by GameInit after construction.
                _particles.Update(world, events, sighted, blasts, steps, trails, LastDrawn, seconds);
            }

            _floating?.Update(world, events, sighted, seconds);
            _texts = _floating is null ? [] : [.. _floating.Texts];
            _removedAnimators.Clear();
            foreach (EntityId id in _animators.Keys)
            {
                if (!seen.ContainsKey(id))
                {
                    _removedAnimators.Add(id);
                }
            }
            foreach (EntityId gone in _removedAnimators)
            {
                _ = _animators.Remove(gone);
            }

            // Particles precede objects on equal keys, as in the original stable sort.
            sprites.InsertRange(0, _particles.Sprites);
            _spriteOrder.Clear();
            for (int i = 0; i < sprites.Count; i++)
            {
                SceneSprite sprite = sprites[i];
                if (spin is WorldSpin turning)
                {
                    (double x, double y) = turning.Transform(sprite.X, sprite.Y);
                    sprite = sprite with
                    {
                        X = x,
                        Y = y
                    };
                    sprites[i] = sprite;
                }

                (double anchorX, double anchorY) = sprite.SortAnchor ?? (sprite.X, sprite.Y);
                double depth = sprite.Pass == int.MaxValue ? 0
                    : Math.Round((anchorX * SceneCamera.CellWidth) - (anchorY * SceneCamera.CellHeight));
                _spriteOrder.Add(new SpriteOrder(i, sprite.Pass, depth));
            }

            _spriteOrder.Sort();
            SceneSprite[] frameSprites = new SceneSprite[sprites.Count];
            for (int i = 0; i < frameSprites.Length; i++)
            {
                frameSprites[i] = sprites[_spriteOrder[i].Index];
            }

            // Only the published snapshot escapes this presenter; working buffers stay private.
            Sprites = Array.AsReadOnly(frameSprites);
        }

        private readonly record struct SpriteOrder(int Index, int Pass, double Depth) : IComparable<SpriteOrder>
        {
            public int CompareTo(SpriteOrder other)
            {
                int comparison = Pass.CompareTo(other.Pass);
                if (comparison == 0)
                {
                    comparison = Depth.CompareTo(other.Depth);
                }

                return comparison == 0 ? Index.CompareTo(other.Index) : comparison;
            }
        }

        /// <summary>
        /// Models the native object render's shadow test: the object sits within 0.1 of a cell
        /// row, and the cell below its rounded column holds a <c>CNT_CMaskResRShadow</c> occupant.
        /// </summary>
        private bool CastsShadow(WorldGrid world, double x, double y)
        {
            double row = Math.Round(y);
            if (Math.Abs(y - row) >= 0.1)
            {
                return false;
            }

            GridPosition below = new((int)Math.Round(x), (int)row + 1);
            return world.Contains(below)
                && world.GetOccupants(below).Any(occupant => occupant.ArchivalIdentity is ArchivalObjectIdentity identity
                    && _library.TryGetLook(identity.ArchivalType, out ObjectLook look)
                    && look.ReceivesShadow);
        }

        /// <summary>The frame an entity's animator last showed, which a just-removed entity still keeps.</summary>
        private (string Page, string Frame)? LastDrawn(EntityId id)
        {
            return _animators.TryGetValue(id, out EntityAnimator? animator) && animator.Frame is string frame
                ? (animator.Page, frame)
                : null;
        }

        /// <summary>
        /// The registration an entity was made from: its archival type, or for a spawned beetle
        /// the original's <c>CNT_OName_SmGr</c> or <c>CNT_OName_SmBlue</c>.
        /// </summary>
        private static string? TypeOf(Entity entity)
        {
            return entity.SmallBeetleBody is SmallBeetleBody beetle
                ? beetle.Color == SmallBeetleColor.Green ? "OSmGr" : "OSmBlue"
                : entity.ArchivalIdentity?.ArchivalType;
        }

        private EntityAnimator? Create(Entity entity)
        {
            if (entity.PlayerBody is not null)
            {
                return new PlayerAnimator(_library, _random);
            }

            if (entity.GreenMonsterBody is not null)
            {
                return new GreenMonsterAnimator(_library, _random);
            }

            if (entity.BlueMonsterBody is not null)
            {
                return new BlueMonsterAnimator(_library, _random);
            }

            if (entity.SmallBeetleBody is SmallBeetleBody beetle)
            {
                return new BeetleAnimator(_library, beetle.Color, _random);
            }

            if (entity.BioMassBody is not null)
            {
                return new BioMassAnimator(_library, _random);
            }

            if (entity.DoorBody is not null)
            {
                return new ObjectAnimator(_library.Table(StartDoorType), _random, 0, "st_doorin", pass: -1);
            }

            if (entity.RocketBody is RocketBody rocket)
            {
                string table = rocket.Direction == Direction.Left ? AnimationLibrary.RocketLeftTable : AnimationLibrary.RocketRightTable;
                return new ObjectAnimator(_library.Table(table), _random, 0, AnimationLibrary.FlyState, pass: 0);
            }

            return entity.BigBombBody is not null
                ? new ObjectAnimator(_library.Table(AnimationLibrary.BigBombTable), _random, 0, AnimationLibrary.StillState, pass: 1)
                : entity.TeleporterBody is not null
                ? new ObjectAnimator(_library.Table(AnimationLibrary.TeleporterTable), _random, 0, AnimationLibrary.StillState, pass: -1)
                : entity.ArchivalIdentity is ArchivalObjectIdentity identity
                && _library.TryGetTable(identity.ArchivalType, out AnimationTable placed)
                ? Placed(identity, placed)
                : null;
        }

        /// <summary>
        /// Animates an archival object. A thrown grenade and a laid mine are created straight into
        /// <c>ST_Work</c>, by <c>GameObjGrenade</c> and <c>GameObjMineMake</c>; a pickup of the same
        /// type rests in <c>ST_Stay</c>.
        /// </summary>
        private ObjectAnimator Placed(ArchivalObjectIdentity identity, AnimationTable table)
        {
            ObjectLook? look = _library.TryGetLook(identity.ArchivalType, out ObjectLook found) ? found : null;
            bool spawnedWeapon = identity.ObjectId == LevelMaterializer.RuntimeObjectId
                && identity.ArchivalType is ThrownGrenadeType or MineType;
            int pass = identity.Kind is EntityKind.Pipe or EntityKind.Tunnel
                || identity.ArchivalType is MessageTableType or MineType
                ? 1
                : 0;
            return new ObjectAnimator(
                table,
                _random,
                identity.Source.EditorIndex,
                spawnedWeapon ? "st_work" : AnimationLibrary.StillState,
                pass,
                randomPhase: identity.Kind is EntityKind.Pipe or EntityKind.Tunnel,
                depth: look?.DepthRange is (double min, double max)
                    ? min + (_random.NextDouble() * (max - min))
                    : 0,
                look: look);
        }
    }
}
