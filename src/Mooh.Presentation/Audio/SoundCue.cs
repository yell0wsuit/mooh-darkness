namespace Mooh.Presentation.Audio
{
    /// <summary>A sound to play this frame, where it happens in the world.</summary>
    /// <param name="Alias">The <c>Sound.ini</c> alias.</param>
    /// <param name="X">The column it happens at.</param>
    /// <param name="Y">The row it happens at.</param>
    /// <param name="Centered">Whether it plays unplaced at full gain, as <c>SndPlay</c> does, rather than <c>ObjSnd</c>'s placement.</param>
    public readonly record struct SoundCue(string Alias, double X, double Y, bool Centered = false);
}
