using System;
using System.Collections.Generic;
using System.Linq;

using Mooh.Core.Enemies;
using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Interactions;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>Shows the recovered <c>CGameObjBioMass</c> state for a simulated bio mass.</summary>
    /// <remarks>
    /// <para>
    /// The bio mass's animation is its behavior, so the simulation already times it: the idle
    /// loop takes the frame time the simulation chose and starts at the phase the simulation is
    /// at. Each bio mass keeps one of three looks for life, <c>ulEInd = App.Rand.Lim(3)</c>.
    /// </para>
    /// <para>
    /// <c>_AEnd_SLook</c> picks the spawn animation before the spawn picks its direction: the
    /// first of left, up, right and down that is empty or holds a bio-strike target decides
    /// between <c>ST_SpawnHor</c> and <c>ST_SpawnVert</c>.
    /// </para>
    /// </remarks>
    public sealed class BioMassAnimator : EntityAnimator
    {
        private const string StayState = "st_stay";
        private const int LookCount = 3;
        private static readonly Direction[] LookOrder = [Direction.Left, Direction.Up, Direction.Right, Direction.Down];
        private readonly int _look;

        /// <summary>Initializes the animator.</summary>
        /// <param name="library">The recovered animation tables.</param>
        /// <param name="random">The source of the original's random look.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public BioMassAnimator(AnimationLibrary library, Random random)
            : base((library ?? throw new ArgumentNullException(nameof(library))).Table(AnimationLibrary.BioMassTable), random)
        {
            _look = Random.Next(LookCount);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<string> PickVariant(IReadOnlyList<IReadOnlyList<string>> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            return candidates.Count == 0 ? [] : candidates[_look % candidates.Count];
        }

        /// <inheritdoc/>
        public override double FrameMilliseconds(AnimationClip clip)
        {
            ArgumentNullException.ThrowIfNull(clip);
            return clip.FrameMilliseconds.Count > 1 ? StayFrameMilliseconds(clip) : clip.FrameMilliseconds[0];
        }

        /// <inheritdoc/>
        public override double StartPosition(AnimationClip clip, IReadOnlyList<string> frames)
        {
            ArgumentNullException.ThrowIfNull(clip);
            ArgumentNullException.ThrowIfNull(frames);
            return clip.FrameMilliseconds.Count < 2 || Entity?.BioMassBody is not { State: BioMassState.Stay } body || body.StayFrameSeconds <= 0
                ? 0
                : Math.Clamp(frames.Count - (body.RemainingSeconds / body.StayFrameSeconds), 0, frames.Count);
        }

        /// <summary>
        /// Loops the idle: <c>_AEnd_SLook</c> re-enters <c>ST_Stay</c> unless the simulation starts
        /// a spawn, which the next update shows.
        /// </summary>
        /// <inheritdoc/>
        public override string? ResolveHandler(string handler)
        {
            return handler == "_AEnd_SLook" ? StayState : null;
        }

        /// <inheritdoc/>
        protected override string? DesiredState(WorldGrid world, Entity entity)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(entity);
            BioMassBody body = entity.BioMassBody ?? throw new ArgumentException("The entity is not a bio mass.", nameof(entity));
            return body.State switch
            {
                BioMassState.Grow => "st_grow",
                BioMassState.Spawn => Animation.State is "st_spawnhor" or "st_spawnvert"
                    ? Animation.State
                    : SpawnState(world, entity),
                BioMassState.Stay => StayState,
                _ => StayState,
            };
        }

        private static string SpawnState(WorldGrid world, Entity entity)
        {
            foreach (Direction direction in LookOrder)
            {
                GridPosition cell = entity.Position.Offset(direction);
                if (!world.Contains(cell))
                {
                    continue;
                }

                IReadOnlyList<Entity> occupants = world.GetOccupants(cell);
                if (occupants.All(occupant => occupant.Interactions.HasFlag(InteractionMask.OccupancyTransparent))
                    || occupants.Any(occupant => occupant.IsBioMassTarget))
                {
                    return direction is Direction.Up or Direction.Down ? "st_spawnvert" : "st_spawnhor";
                }
            }

            return "st_spawnhor";
        }

        private double StayFrameMilliseconds(AnimationClip clip)
        {
            return Entity?.BioMassBody is { StayFrameSeconds: > 0 } body
                ? body.StayFrameSeconds * 1000
                : clip.FrameMilliseconds[Random.Next(clip.FrameMilliseconds.Count)];
        }
    }
}
