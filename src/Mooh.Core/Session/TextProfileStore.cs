using System;
using System.Text.Json;

namespace Mooh.Core.Session
{
    /// <summary>Stores the same profile JSON through host-provided text storage.</summary>
    public sealed class TextProfileStore(Func<string?> read, Action<string> write) : IPlayerProfileStore
    {
        /// <inheritdoc/>
        public PlayerProfile Load()
        {
            string? value = read();
            if (string.IsNullOrWhiteSpace(value))
            {
                return new PlayerProfile();
            }
            try
            {
                PlayerProfile profile = JsonSerializer.Deserialize(value, PlayerProfileJsonContext.Default.PlayerProfile) ?? new PlayerProfile();
                if (profile.Version != 1)
                {
                    return new PlayerProfile();
                }
                profile.Normalize();
                return profile;
            }
            catch (JsonException)
            {
                return new PlayerProfile();
            }
        }

        /// <inheritdoc/>
        public void Save(PlayerProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            profile.Normalize();
            write(JsonSerializer.Serialize(profile, PlayerProfileJsonContext.Default.PlayerProfile));
        }
    }
}
