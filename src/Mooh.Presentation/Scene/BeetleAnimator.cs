using System;
using System.Collections.Generic;

using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>Shows the recovered <c>CGameObjSmBeetle</c> flight for a simulated small beetle.</summary>
    /// <remarks>
    /// A beetle picks one of its color's two variants for life, <c>ulEInd = App.Rand.Lim(2)</c>,
    /// and starts flying left or right at random. Every state change starts at a random phase of
    /// <c>App.Rand.FLim(len(SObj.AInds))</c> — the variant count, not the frame count, so only the
    /// first two frames are ever a starting frame.
    /// </remarks>
    public sealed class BeetleAnimator : EntityAnimator
    {
        private readonly int _variant;
        private Direction _direction;

        /// <summary>Initializes the animator.</summary>
        /// <param name="library">The recovered animation tables.</param>
        /// <param name="color">The beetle's color.</param>
        /// <param name="random">The source of the original's random choices.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public BeetleAnimator(AnimationLibrary library, SmallBeetleColor color, Random random)
            : base(
                (library ?? throw new ArgumentNullException(nameof(library))).Table(
                    color == SmallBeetleColor.Green ? AnimationLibrary.GreenBeetleTable : AnimationLibrary.BlueBeetleTable),
                random)
        {
            _variant = Random.Next(Table.States["st_flyleft"].Variants.Count);
            _direction = Random.Next(2) == 0 ? Direction.Left : Direction.Right;
        }

        /// <inheritdoc/>
        public override IReadOnlyList<string> PickVariant(IReadOnlyList<IReadOnlyList<string>> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            return candidates.Count == 0 ? [] : candidates[_variant % candidates.Count];
        }

        /// <inheritdoc/>
        public override double StartPosition(AnimationClip clip, IReadOnlyList<string> frames)
        {
            ArgumentNullException.ThrowIfNull(clip);
            return Random.NextDouble() * clip.Variants.Count;
        }

        /// <inheritdoc/>
        protected override string? DesiredState(WorldGrid world, Entity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            _direction = entity.Motion?.TravelDirection ?? _direction;
            return "st_fly" + StateNames.Name(_direction);
        }
    }
}
