using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mooh.Core.Session
{
    /// <summary>Loads profile data and replaces its JSON file atomically within the user's data directory.</summary>
    public sealed class PlayerProfileStore : IPlayerProfileStore
    {
        /// <summary>Creates a store in the supplied user-data directory.</summary>
        /// <param name="userDataDirectory">The directory reserved for this user's game data.</param>
        public PlayerProfileStore(string userDataDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userDataDirectory);
            FilePath = Path.Combine(Path.GetFullPath(userDataDirectory), "player-profile.json");
        }

        /// <summary>Gets the profile's absolute JSON file path.</summary>
        public string FilePath
        {
            get;
        }

        /// <summary>Loads and normalizes the profile, returning defaults for missing, unreadable or invalid data.</summary>
        /// <returns>A usable profile.</returns>
        public PlayerProfile Load()
        {
            try
            {
                using FileStream stream = File.OpenRead(FilePath);
                PlayerProfile profile = JsonSerializer.Deserialize(stream, PlayerProfileJsonContext.Default.PlayerProfile) ?? new PlayerProfile();
                if (profile.Version != 1)
                {
                    return new PlayerProfile();
                }

                profile.Normalize();
                return profile;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return new PlayerProfile();
            }
        }

        /// <summary>Writes a normalized profile to a flushed temporary file and atomically replaces the previous file.</summary>
        /// <param name="profile">The profile to save.</param>
        public void Save(PlayerProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            profile.Normalize();
            string directory = Path.GetDirectoryName(FilePath)!;
            _ = Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "." + SaveDataLocation.Name + "-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    JsonSerializer.Serialize(stream, profile, PlayerProfileJsonContext.Default.PlayerProfile);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporary, FilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
    [JsonSerializable(typeof(PlayerProfile))]
    internal sealed partial class PlayerProfileJsonContext : JsonSerializerContext
    {
    }
}
