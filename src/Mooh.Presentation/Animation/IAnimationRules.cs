using System.Collections.Generic;

namespace Mooh.Presentation.Animation
{
    /// <summary>
    /// The per-class choices the original makes when an object enters a state: which variant list
    /// it plays, which variant of that list, how fast, and where its gameplay end handlers lead.
    /// </summary>
    public interface IAnimationRules
    {
        /// <summary>
        /// Gets the variant list a state plays now. The player picks its flying frames here; an
        /// unchanged list is how a state re-entering itself keeps playing.
        /// </summary>
        /// <param name="state">The state being entered.</param>
        /// <param name="clip">The state's clip.</param>
        /// <returns>The candidate variants, by reference from <paramref name="clip"/>.</returns>
        IReadOnlyList<IReadOnlyList<string>> Candidates(string state, AnimationClip clip);

        /// <summary>
        /// Picks one variant: at random for the player, by editor index for a placed object.
        /// </summary>
        /// <param name="candidates">The list <see cref="Candidates"/> returned.</param>
        /// <returns>The frames to play, or an empty list for a state that draws nothing.</returns>
        IReadOnlyList<string> PickVariant(IReadOnlyList<IReadOnlyList<string>> candidates);

        /// <summary>Gets the frame time a state plays at, choosing among recovered alternatives.</summary>
        /// <param name="clip">The state's clip.</param>
        /// <returns>The milliseconds per frame; zero for a still clip.</returns>
        double FrameMilliseconds(AnimationClip clip);

        /// <summary>
        /// Resolves an end handler the presentation can run on its own, such as the player's
        /// ambient counters. Gameplay handlers stay with the simulation.
        /// </summary>
        /// <param name="handler">The recovered handler name, such as <c>_AEnd_Rot</c>.</param>
        /// <returns>The state to enter, or <see langword="null"/> to hold the last frame.</returns>
        string? ResolveHandler(string handler);
    }
}
