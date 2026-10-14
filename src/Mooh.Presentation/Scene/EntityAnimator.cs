using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Simulation;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// Animates one simulated entity with its class's recovered rules.
    /// </summary>
    /// <remarks>
    /// The simulation owns every gameplay transition. Each update derives the state the original
    /// would be in and enters it when that changes; chains the original runs from its animation
    /// table alone play on by themselves in between, and a gameplay end handler holds its last
    /// frame until the simulation moves on.
    /// </remarks>
    public abstract class EntityAnimator : IAnimationRules
    {
        private string? _desired;

        /// <summary>Initializes the animator.</summary>
        /// <param name="table">The entity's recovered state table.</param>
        /// <param name="random">The source of the original's random choices.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        protected EntityAnimator(AnimationTable table, Random random)
        {
            ArgumentNullException.ThrowIfNull(table);
            ArgumentNullException.ThrowIfNull(random);
            Table = table;
            Random = random;
            Animation = new AnimationPlayer(table, this);
        }

        /// <summary>Gets the entity's animation.</summary>
        public AnimationPlayer Animation
        {
            get;
        }

        /// <summary>Gets the page the current frame draws from.</summary>
        public virtual string Page => Animation.Page;

        /// <summary>Gets the frame to draw, or <see langword="null"/> while the entity is hidden.</summary>
        public virtual string? Frame => Animation.Frame;

        /// <summary>
        /// Gets the shadow frame the state casts, its <c>DInds</c> at the animation's own index
        /// (<c>DInds[(int)fAInd % len]</c>), or <see langword="null"/> when it casts none.
        /// </summary>
        public virtual (string Page, string Frame)? Shadow => Animation.Clip is { Shadow: { Count: > 0 } frames, ShadowPage: string page }
            && Frame is not null
                ? (page, frames[(int)Animation.Position % frames.Count])
                : null;

        /// <summary>Gets the frames drawn additively over the entity's own, in order.</summary>
        /// <param name="clockMilliseconds">The game's clock, which a glow steps with.</param>
        /// <returns>The pages and frames.</returns>
        public virtual IReadOnlyList<(string Page, string Frame)> Overlays(double clockMilliseconds)
        {
            return [];
        }

        /// <summary>Gets how far from its cell coordinate the entity draws, in cells.</summary>
        public virtual (double X, double Y) Offset => (0, 0);

        /// <summary>Gets the original's render pass, <c>RPass</c>, for the current state.</summary>
        public virtual int Pass => 0;

        /// <summary>Gets the entity's recovered state table.</summary>
        protected AnimationTable Table
        {
            get;
        }

        /// <summary>Gets the source of the original's random choices.</summary>
        protected Random Random
        {
            get;
        }

        /// <summary>Gets the world of the current update.</summary>
        protected WorldGrid? World
        {
            get; private set;
        }

        /// <summary>Gets the entity of the current update.</summary>
        protected Entity? Entity
        {
            get; private set;
        }

        /// <summary>Gets a value indicating whether the clip stands still, as a frozen monster's does.</summary>
        protected virtual bool IsPaused => false;

        /// <summary>Advances the animation by a frame of simulation, then follows the entity.</summary>
        /// <param name="world">The simulated world.</param>
        /// <param name="entity">The animated entity.</param>
        /// <param name="events">The events the simulation reported for this frame.</param>
        /// <param name="seconds">The simulated time of this frame.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public void Update(WorldGrid world, Entity entity, IReadOnlyList<SimulationEvent> events, double seconds)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(events);
            World = world;
            Entity = entity;

            if (!IsPaused)
            {
                Animation.Advance(seconds);
            }

            string? desired = DesiredState(world, entity);
            if (desired is not null && (desired != _desired || Animation.State is null))
            {
                _desired = desired;
                Animation.Play(desired);
            }

            React(events);
        }

        /// <inheritdoc/>
        public virtual IReadOnlyList<IReadOnlyList<string>> Candidates(string state, AnimationClip clip)
        {
            ArgumentNullException.ThrowIfNull(clip);
            return clip.Variants;
        }

        /// <inheritdoc/>
        public virtual IReadOnlyList<string> PickVariant(IReadOnlyList<IReadOnlyList<string>> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            return candidates.Count == 0 ? [] : candidates[0];
        }

        /// <inheritdoc/>
        public virtual double FrameMilliseconds(AnimationClip clip)
        {
            ArgumentNullException.ThrowIfNull(clip);
            return clip.FrameMilliseconds[0];
        }

        /// <inheritdoc/>
        public virtual double StartPosition(AnimationClip clip, IReadOnlyList<string> frames)
        {
            return 0;
        }

        /// <inheritdoc/>
        public virtual string? ResolveHandler(string handler)
        {
            return null;
        }

        /// <summary>
        /// Gets the state the original would be in for what the simulation's entity is doing.
        /// </summary>
        /// <param name="world">The simulated world.</param>
        /// <param name="entity">The animated entity.</param>
        /// <returns>The state, or <see langword="null"/> to let the playing chain continue.</returns>
        protected abstract string? DesiredState(WorldGrid world, Entity entity);

        /// <summary>Starts the transient states the frame's events call for.</summary>
        /// <param name="events">The events the simulation reported for this frame.</param>
        protected virtual void React(IReadOnlyList<SimulationEvent> events)
        {
        }
    }
}
