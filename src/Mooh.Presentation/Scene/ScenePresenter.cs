using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Explosions;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;

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
        private readonly AnimationLibrary _library;
        private readonly Random _random;
        private List<SceneSprite> _sprites = [];

        /// <summary>Initializes the presenter.</summary>
        /// <param name="library">The recovered animation tables.</param>
        /// <param name="random">The source of the original's presentation-only random choices.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public ScenePresenter(AnimationLibrary library, Random random)
        {
            ArgumentNullException.ThrowIfNull(library);
            ArgumentNullException.ThrowIfNull(random);
            _library = library;
            _random = random;
        }

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
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public void Update(WorldGrid world, IReadOnlyList<SimulationEvent> events, double seconds)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(events);

            HashSet<EntityId> present = [];
            List<SceneSprite> sprites = [];
            foreach (Entity entity in world.Entities)
            {
                _ = present.Add(entity.Id);
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
                if (animator.Frame is string frame)
                {
                    (double x, double y) = DrawnPosition.Of(entity);
                    sprites.Add(new SceneSprite(entity.Id, animator.Page, frame, x, y, animator.Pass));
                }
            }

            foreach (EntityId gone in _animators.Keys.Where(id => !present.Contains(id)).ToList())
            {
                _ = _animators.Remove(gone);
            }

            // The native ListRenderSort comparator: the pass, then the screen x - y, so tiles whose
            // faces reach up and right overlap their lower-left neighbors. OrderBy is stable, so
            // the world's own order breaks ties as the object addresses did.
            _sprites = [.. sprites
                .OrderBy(sprite => sprite.Pass)
                .ThenBy(sprite => Math.Round((sprite.X * SceneCamera.CellWidth) - (sprite.Y * SceneCamera.CellHeight)))];
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
                randomPhase: identity.Kind is EntityKind.Pipe or EntityKind.Tunnel);
        }
    }
}
