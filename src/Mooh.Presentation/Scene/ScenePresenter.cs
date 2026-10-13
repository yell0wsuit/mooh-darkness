using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// Keeps one animator per simulated entity and turns the world into an ordered draw list.
    /// </summary>
    /// <remarks>
    /// The presenter only reads the simulation. Each update advances every animator by the frame's
    /// simulated time, then lists the frames to draw in ascending render pass; within a pass the
    /// world's own order stands, as the native object list draws.
    /// </remarks>
    public sealed class ScenePresenter
    {
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

            _sprites = [.. sprites.OrderBy(sprite => sprite.Pass)];
        }

        private EntityAnimator? Create(Entity entity)
        {
            return entity.PlayerBody is not null
                ? new PlayerAnimator(_library, _random)
                : entity.GreenMonsterBody is not null
                ? new GreenMonsterAnimator(_library, _random)
                : entity.BlueMonsterBody is not null ? new BlueMonsterAnimator(_library, _random) : null;
        }
    }
}
