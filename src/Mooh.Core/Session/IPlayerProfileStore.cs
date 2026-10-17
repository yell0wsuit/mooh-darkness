namespace Mooh.Core.Session
{
    /// <summary>Persists the normalized player profile through a host's storage.</summary>
    public interface IPlayerProfileStore
    {
        /// <summary>Loads a valid profile or returns defaults.</summary>
        PlayerProfile Load();

        /// <summary>Persists progress, options and scores.</summary>
        void Save(PlayerProfile profile);
    }
}
