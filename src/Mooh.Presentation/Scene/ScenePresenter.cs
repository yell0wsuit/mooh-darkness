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
        private readonly FloatingTexts _floating;
        private List<SceneText> _texts = [];
        private Dictionary<EntityId, Sighting> _seen = [];
        private double _clockMilliseconds;
        private readonly AnimationLibrary _library;
        private readonly Random _random;
        private List<SceneSprite> _sprites = [];
        private List<SoundCue> _sounds = [];

        /// <summary>Initializes the presenter.</summary>
        /// <param name="library">The recovered animation tables.</param>
        /// <param name="random">The source of the original's presentation-only random choices.</param>
        /// <param name="strings">The recovered text table the floating lines read, if loaded.</param>
        /// <exception cref="ArgumentNullException"><paramref name="library"/> or <paramref name="random"/> is <see langword="null"/>.</exception>
        public ScenePresenter(AnimationLibrary library, Random random, StringTable? strings = null)
        {
            ArgumentNullException.ThrowIfNull(library);
            ArgumentNullException.ThrowIfNull(random);
            _library = library;
            _random = random;
            _effects = new EffectSystem(library, random);
            _particles = new ParticleSystem(library, random);
            _floating = new FloatingTexts(library, strings, random);
            _eventSounds = new EventSounds(library);
        }

        /// <summary>Gets the sounds cued this update, in order.</summary>
        public IReadOnlyList<SoundCue> Sounds => _sounds;

        /// <summary>Gets how far the view is jerked off its place, in screen pixels.</summary>
        public (double X, double Y) Shake => (_shake.X, _shake.Y);

        /// <summary>Gets the floating scores and lines, drawn over the world, in order.</summary>
        public IReadOnlyList<SceneText> Texts => _texts;

        /// <summary>Gets the frames to draw, in draw order.</summary>
        public IReadOnlyList<SceneSprite> Sprites => _sprites;

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
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public void Update(WorldGrid world, IReadOnlyList<SimulationEvent> events, double seconds, WorldSpin? spin = null)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(events);

            _clockMilliseconds += seconds * 1000;
            HashSet<EntityId> present = [];
            Dictionary<EntityId, Sighting> seen = [];
            List<SceneSprite> sprites = [];
            List<SoundCue> sounds = [];
            List<(double X, double Y)> steps = [];
            foreach (Entity entity in world.Entities)
            {
                _ = present.Add(entity.Id);
                (double seenX, double seenY) = DrawnPosition.Of(entity);
                seen[entity.Id] = new Sighting(TypeOf(entity), seenX, seenY, entity.PlayerBody is not null, entity.BigBombBody is not null);
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
                for (int step = animator.TakeSteps(); step > 0; step--)
                {
                    steps.Add(DrawnPosition.Of(entity));
                }

                foreach (string alias in animator.TakeSounds())
                {
                    (double soundX, double soundY) = DrawnPosition.Of(entity);
                    sounds.Add(new SoundCue(alias, soundX, soundY));
                }

                if (animator.Frame is string frame)
                {
                    (double x, double y) = DrawnPosition.Of(entity);
                    (double dx, double dy) = animator.Offset;
                    if (animator.Shadow is (string shadowPage, string shadowFrame) && CastsShadow(world, x, y))
                    {
                        sprites.Add(new SceneSprite(entity.Id, shadowPage, shadowFrame, x + dx, y + dy, animator.Pass));
                    }

                    sprites.Add(new SceneSprite(entity.Id, animator.Page, frame, x + dx, y + dy, animator.Pass));
                    foreach ((string overlayPage, string overlayFrame) in animator.Overlays(_clockMilliseconds))
                    {
                        sprites.Add(new SceneSprite(entity.Id, overlayPage, overlayFrame, x + dx, y + dy, animator.Pass, Additive: true));
                    }
                }
            }

            // An event's entity may be gone by now; it sounds where it was last drawn.
            Dictionary<EntityId, Sighting> sighted = [with(_seen)];
            foreach ((EntityId id, Sighting sighting) in seen)
            {
                sighted[id] = sighting;
            }

            sounds.AddRange(_eventSounds.For(events, sighted));
            _shake.Update(world, events, sighted, seconds, _random);
            _seen = seen;
            _sounds = sounds;
            IReadOnlyList<GridPosition> blasts = _effects.Update(world, events, seconds);
            sprites.AddRange(_effects.Sprites);
            _particles.Update(world, events, sighted, blasts, steps, seconds);
            _floating.Update(world, events, sighted, seconds);
            _texts = [.. _floating.Texts];
            foreach (EntityId gone in _animators.Keys.Where(id => !present.Contains(id)).ToList())
            {
                _ = _animators.Remove(gone);
            }

            if (spin is WorldSpin turning)
            {
                sprites = [.. sprites.Select(sprite =>
                {
                    (double x, double y) = turning.Transform(sprite.X, sprite.Y);
                    return sprite with { X = x, Y = y };
                })];
            }

            // The native ListRenderSort comparator: the pass, then the screen x - y, so tiles whose
            // faces reach up and right overlap their lower-left neighbors. OrderBy is stable, so
            // the world's own order breaks ties as the object addresses did.
            // The particles are drawn over the sorted world, the overlay parts last of all.
            IEnumerable<SceneSprite> particles = _particles.Sprites;
            if (spin is WorldSpin spinning)
            {
                particles = particles.Select(sprite =>
                {
                    (double x, double y) = spinning.Transform(sprite.X, sprite.Y);
                    return sprite with
                    {
                        X = x,
                        Y = y
                    };
                });
            }

            _sprites = [.. sprites
                .OrderBy(sprite => sprite.Pass)
                .ThenBy(sprite => Math.Round((sprite.X * SceneCamera.CellWidth) - (sprite.Y * SceneCamera.CellHeight))),
                .. particles];
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
