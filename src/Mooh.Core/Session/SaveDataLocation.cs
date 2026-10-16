using System;
using System.IO;

namespace Mooh.Core.Session
{
    /// <summary>Chooses the same portable, Documents and local-data save-folder order as the other DX ports.</summary>
    public static class SaveDataLocation
    {
        /// <summary>The save folder name.</summary>
        public const string Name = "MoohDarkness_SaveData";

        /// <summary>Finds and probes the platform's default save directory.</summary>
        /// <returns>A writable save directory.</returns>
        public static string DefaultDirectory()
        {
            return ResolveDirectory(AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), TryWritable);
        }

        /// <summary>Selects a writable folder, skipping the executable directory within a signed macOS application bundle.</summary>
        /// <param name="executableDirectory">The executable's directory.</param>
        /// <param name="documentsDirectory">The user's Documents directory.</param>
        /// <param name="localDataDirectory">The user's local application-data directory.</param>
        /// <param name="ensureWritable">Creates and probes a candidate directory.</param>
        /// <returns>The selected save directory, or the current directory if all candidates fail.</returns>
        public static string ResolveDirectory(string executableDirectory, string documentsDirectory, string localDataDirectory, Func<string, bool> ensureWritable)
        {
            ArgumentNullException.ThrowIfNull(ensureWritable);
            bool bundled = false;
            for (DirectoryInfo? directory = new(executableDirectory); directory is not null; directory = directory.Parent)
            {
                if (directory.Name.Equals("MacOS", StringComparison.OrdinalIgnoreCase)
                    && directory.Parent?.Name.Equals("Contents", StringComparison.OrdinalIgnoreCase) == true
                    && directory.Parent.Parent?.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase) == true)
                {
                    bundled = true;
                    break;
                }
            }
            return !bundled && ensureWritable(Path.Combine(executableDirectory, Name))
                ? Path.Combine(executableDirectory, Name)
                : !string.IsNullOrEmpty(documentsDirectory) && ensureWritable(Path.Combine(documentsDirectory, Name))
                ? Path.Combine(documentsDirectory, Name)
                : !string.IsNullOrEmpty(localDataDirectory) && ensureWritable(Path.Combine(localDataDirectory, Name))
                ? Path.Combine(localDataDirectory, Name)
                : ".";
        }

        private static bool TryWritable(string path)
        {
            try
            {
                _ = Directory.CreateDirectory(path);
                string probe = Path.Combine(path, ".write-test-" + Guid.NewGuid().ToString("N"));
                using FileStream stream = new(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
                stream.WriteByte(0);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return false;
            }
        }
    }
}
