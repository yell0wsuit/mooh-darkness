namespace Mooh.Core.Simulation
{
    /// <summary>Identifies whether a level has been decided.</summary>
    public enum LevelOutcome
    {
        /// <summary>The level is still being played.</summary>
        InProgress,

        /// <summary>Every level-end lock carrier was destroyed.</summary>
        Won,

        /// <summary>Player health reached zero.</summary>
        Lost,
    }
}
