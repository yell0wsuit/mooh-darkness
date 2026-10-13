using System;
using System.Collections.Generic;

namespace Mooh.Presentation.Animation
{
    /// <summary>
    /// Plays one object's clips with the original's rules.
    /// </summary>
    /// <remarks>
    /// The native list advances <c>fAInd</c> by <c>fASpeed</c> (frames per millisecond) and fires
    /// <c>OnAnimEnd</c> when it passes the last frame. A state that ends into itself, or has no end
    /// at all, wraps and keeps its overshoot. <c>StateSet</c> into a different state, or into the
    /// same state with a different variant list (the player's flying frames), resets
    /// <c>fAInd</c> to zero and so drops the overshoot. A state with no frames still lasts one
    /// frame time: that is how the player waits, hidden, in the start door and the shelter.
    /// </remarks>
    public sealed class AnimationPlayer
    {
        private readonly IAnimationRules _rules;
        private readonly AnimationTable _table;
        private IReadOnlyList<IReadOnlyList<string>>? _candidates;
        private double _frameMilliseconds;

        /// <summary>Initializes a player over one table.</summary>
        /// <param name="table">The object's recovered state table.</param>
        /// <param name="rules">The object class's variant, speed and handler rules.</param>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        public AnimationPlayer(AnimationTable table, IAnimationRules rules)
        {
            ArgumentNullException.ThrowIfNull(table);
            ArgumentNullException.ThrowIfNull(rules);
            _table = table;
            _rules = rules;
        }

        /// <summary>Gets the playing state, or <see langword="null"/> before the first.</summary>
        public string? State
        {
            get; private set;
        }

        /// <summary>Gets the playing state's clip.</summary>
        public AnimationClip? Clip
        {
            get; private set;
        }

        /// <summary>Gets the page the playing clip draws from.</summary>
        public string Page => Clip?.Page ?? _table.Page;

        /// <summary>Gets the playing variant's frames; empty while the object is hidden.</summary>
        public IReadOnlyList<string> Frames { get; private set; } = [];

        /// <summary>Gets the fractional frame position, the original's <c>fAInd</c>.</summary>
        public double Position
        {
            get; private set;
        }

        /// <summary>
        /// Gets a value indicating whether the clip reached a gameplay end handler and holds its last
        /// frame until the simulation moves the object on.
        /// </summary>
        public bool IsHeld
        {
            get; private set;
        }

        /// <summary>Gets the frame to draw, or <see langword="null"/> while hidden.</summary>
        public string? Frame => Frames.Count == 0
            ? null
            : Frames[Math.Min((int)Position, Frames.Count - 1)];

        private double Length => Math.Max(Frames.Count, 1);

        /// <summary>
        /// Enters a state. Re-entering the playing state with an unchanged variant list keeps its
        /// position, as <c>StateSet</c> does.
        /// </summary>
        /// <param name="state">The recovered state name.</param>
        /// <exception cref="KeyNotFoundException">The table has no such state.</exception>
        public void Play(string state)
        {
            AnimationClip clip = _table.States[state];
            IReadOnlyList<IReadOnlyList<string>> candidates = _rules.Candidates(state, clip);
            if (state == State && ReferenceEquals(candidates, _candidates))
            {
                return;
            }

            State = state;
            Clip = clip;
            _candidates = candidates;
            Frames = _rules.PickVariant(candidates);
            _frameMilliseconds = _rules.FrameMilliseconds(clip);
            Position = _rules.StartPosition(clip, Frames);
            IsHeld = false;
        }

        /// <summary>Restarts the playing state at its first frame, as a monster's <c>IceOff</c> does.</summary>
        public void Restart()
        {
            Position = 0;
            IsHeld = false;
        }

        /// <summary>Advances the playing clip, following its ends.</summary>
        /// <param name="seconds">The elapsed time.</param>
        public void Advance(double seconds)
        {
            if (Clip is null || IsHeld || _frameMilliseconds <= 0 || seconds <= 0)
            {
                return;
            }

            Position += seconds * 1000 / _frameMilliseconds;
            while (Position >= Length)
            {
                string? next = Clip.Next
                    ?? (Clip.EndHandler is string handler ? _rules.ResolveHandler(handler) : State);
                if (next is null)
                {
                    IsHeld = true;
                    Position = Length - 1;
                    return;
                }

                string? before = State;
                IReadOnlyList<IReadOnlyList<string>>? candidates = _candidates;
                Play(next);
                if (State != before || !ReferenceEquals(candidates, _candidates))
                {
                    return;
                }

                Position -= Length;
            }
        }
    }
}
