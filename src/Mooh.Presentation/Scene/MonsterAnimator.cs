using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// Shares what the Green and Blue Monsters do alike: a loop starts at a random phase, and the
    /// ice bonus swaps in a still frame until it thaws.
    /// </summary>
    /// <remarks>
    /// <c>StateSet</c> starts a state with no <c>AEnd</c> at <c>App.Rand.FLim(len(AInds))</c>.
    /// <c>IceOn</c> sets <c>AInds = IceAnims[ulDDir]</c> and <c>fASpeed = 0</c>; <c>IceOff</c>
    /// restores the state's frames from <c>fAInd = 0</c>.
    /// </remarks>
    /// <param name="table">The monster's recovered state table.</param>
    /// <param name="random">The source of the original's random phase.</param>
    /// <param name="iceFrames">The ice frames by facing: left, up, right, down.</param>
    public abstract class MonsterAnimator(AnimationTable table, Random random, IReadOnlyList<string> iceFrames) : EntityAnimator(table, random)
    {
        private readonly IReadOnlyList<string> _iceFrames = iceFrames;
        private bool _wasFrozen;
        private bool _justThawed;

        /// <inheritdoc/>
        public override string? Frame => Entity is not null && IsFrozen(Entity)
            ? _iceFrames[(int)Facing(Entity)]
            : base.Frame;

        /// <inheritdoc/>
        protected override bool IsPaused => _justThawed || (Entity is not null && IsFrozen(Entity));

        /// <inheritdoc/>
        public override double StartPosition(AnimationClip clip, IReadOnlyList<string> frames)
        {
            ArgumentNullException.ThrowIfNull(clip);
            ArgumentNullException.ThrowIfNull(frames);
            return clip.Next is null && clip.EndHandler is null ? Random.NextDouble() * frames.Count : 0;
        }

        /// <inheritdoc/>
        protected sealed override string? DesiredState(WorldGrid world, Entity entity)
        {
            bool frozen = IsFrozen(entity);
            _wasFrozen = frozen;
            return frozen && Animation.State is not null ? null : MonsterState(entity);
        }

        /// <inheritdoc/>
        protected override void Tick(double seconds)
        {
            _justThawed = _wasFrozen && Entity is not null && !IsFrozen(Entity);
            if (_justThawed)
            {
                // The saved phase restarts before advancing, and frozen time cannot charge it.
                Animation.Restart();
            }
        }

        /// <summary>Gets the state for the monster's simulated phase.</summary>
        /// <param name="entity">The monster.</param>
        /// <returns>The state, or <see langword="null"/> to let the playing chain continue.</returns>
        protected abstract string? MonsterState(Entity entity);

        /// <summary>Gets whether the monster is frozen.</summary>
        /// <param name="entity">The monster.</param>
        /// <returns><see langword="true"/> while the ice bonus holds it.</returns>
        protected abstract bool IsFrozen(Entity entity);

        /// <summary>Gets the monster's facing.</summary>
        /// <param name="entity">The monster.</param>
        /// <returns>The facing direction.</returns>
        protected abstract Direction Facing(Entity entity);
    }
}
