using System.Collections.Generic;

namespace Mooh.Presentation.Audio
{
    /// <summary>One <c>Sound.ini</c> alias.</summary>
    /// <param name="Alias">The alias the game plays.</param>
    /// <param name="Files">The sound files, without extension; one is picked per play. None is silent.</param>
    /// <param name="Buffers">How many of the alias may play at once.</param>
    /// <param name="Volume">The volume range a play picks from.</param>
    /// <param name="Frequency">The playback rate range a play picks from.</param>
    public sealed record SoundDefinition(
        string Alias,
        IReadOnlyList<string> Files,
        int Buffers,
        (double Min, double Max) Volume,
        (double Min, double Max) Frequency);
}
