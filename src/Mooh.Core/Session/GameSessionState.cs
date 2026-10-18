namespace Mooh.Core.Session
{
    /// <summary>Identifies what a session is currently waiting for.</summary>
    public enum GameSessionState
    {
        /// <summary>The current level is being played.</summary>
        Playing,

        /// <summary>The current level was completed and a further level remains in the pack.</summary>
        LevelCompleted,

        /// <summary>The current level was failed. The original offers no lives, only a replay.</summary>
        LevelFailed,

        /// <summary>The last level in the pack was completed.</summary>
        CampaignCompleted,
    }
}
