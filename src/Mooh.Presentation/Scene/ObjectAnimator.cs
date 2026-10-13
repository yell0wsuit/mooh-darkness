using System;
using System.Collections.Generic;

using Mooh.Core.Entities;
using Mooh.Core.Grid;
using Mooh.Core.Levels;
using Mooh.Core.Player;
using Mooh.Presentation.Animation;

namespace Mooh.Presentation.Scene
{
    /// <summary>
    /// Shows a placed or spawned object's recovered state: terrain, pipes, pickups, hazards, the
    /// start door, the message table and the weapons the player leaves behind.
    /// </summary>
    /// <remarks>
    /// A placed object picks its variant by editor index, <c>AInds[EInd % len(AInds)]</c>, and a
    /// pair of frame times is a uniform choice of speed between them, <c>App.Rand.FRange</c>.
    /// Every state starts at its first frame, except a pipe's loop, which starts at a random
    /// phase.
    /// </remarks>
    /// <param name="table">The object's recovered state table.</param>
    /// <param name="random">The source of the original's random choices.</param>
    /// <param name="editorIndex">The editor index, <c>EInd</c>; zero for a spawned object.</param>
    /// <param name="initialState">The state the object is created in.</param>
    /// <param name="pass">The object type's render pass, <c>RPass</c>.</param>
    /// <param name="randomPhase">Whether a loop starts at a random phase, as a pipe's does.</param>
    public sealed class ObjectAnimator(
        AnimationTable table,
        Random random,
        int editorIndex,
        string initialState,
        int pass,
        bool randomPhase = false) : EntityAnimator(table, random)
    {
        /// <summary>The big bomb's tick handler, which re-enters its work loop until the fuse ends.</summary>
        private const string BigBombTickHandler = "BBombTick";

        /// <inheritdoc/>
        public override int Pass => pass;

        /// <inheritdoc/>
        public override IReadOnlyList<string> PickVariant(IReadOnlyList<IReadOnlyList<string>> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            return candidates.Count == 0 ? [] : candidates[editorIndex % candidates.Count];
        }

        /// <inheritdoc/>
        public override double FrameMilliseconds(AnimationClip clip)
        {
            ArgumentNullException.ThrowIfNull(clip);
            if (clip.FrameMilliseconds.Count < 2)
            {
                return clip.FrameMilliseconds[0];
            }

            double fast = 1.0 / clip.FrameMilliseconds[0];
            double slow = 1.0 / clip.FrameMilliseconds[1];
            return 1.0 / (fast + (Random.NextDouble() * (slow - fast)));
        }

        /// <inheritdoc/>
        public override double StartPosition(AnimationClip clip, IReadOnlyList<string> frames)
        {
            ArgumentNullException.ThrowIfNull(clip);
            ArgumentNullException.ThrowIfNull(frames);
            return randomPhase && !clip.IsStill ? Random.NextDouble() * frames.Count : 0;
        }

        /// <inheritdoc/>
        public override string? ResolveHandler(string handler)
        {
            return handler == BigBombTickHandler ? "st_work" : null;
        }

        /// <inheritdoc/>
        protected override string? DesiredState(WorldGrid world, Entity entity)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(entity);
            return entity switch
            {
                { BalloonFuseBody.HasBeenLit: true } => "st_work",
                { FlyingArrowBody.IsFlying: true } => "st_arrowfly",
                { FlyingArrowBody.IsLaunched: true } => "st_work",
                { WorldRotationBody.WindupRemainingSeconds: not null } => "st_work",
                { MessageTableBody.HasBeenRead: true } => "st_work",
                { KeyDoorBody.OpeningRemainingSeconds: not null } => "st_work",
                { DoorBody: DoorBody door } => DoorState(world, door),
                _ => initialState,
            };
        }

        /// <summary>
        /// Follows the player's door actions. <c>GameInit</c> enters <c>ST_DoorIn</c>, which opens
        /// and stays open; <c>_AEnd_GStartCloseAndStay</c> closes the unused door and lets it go.
        /// A shelter visit opens the door, closes it behind the hidden player, opens it again for
        /// the way out and closes it once the player stands outside.
        /// </summary>
        private string? DoorState(WorldGrid world, DoorBody door)
        {
            if (door.RemovalRemainingSeconds is not null)
            {
                return "st_doorcloseandout";
            }

            PlayerActionKind? action = world.Player?.PlayerBody?.Action?.Kind;
            return action == PlayerActionKind.LevelEntry
                ? "st_doorin"
                : action is PlayerActionKind.ShelterIn or PlayerActionKind.ShelterOut
                ? "st_dooropen"
                : action == PlayerActionKind.ShelterHidden || door.IsShelter
                ? "st_doorclose"
                : Animation.State is null ? initialState : null;
        }
    }
}
