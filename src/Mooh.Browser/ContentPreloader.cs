using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Mooh.Browser
{
    /// <summary>Populates WASM's virtual filesystem with the original runtime asset paths.</summary>
    internal static class ContentPreloader
    {
        public static async Task<string> LoadAsync()
        {
            using HttpClient client = new()
            {
                BaseAddress = new Uri(PageInterop.BaseUri())
            };
            byte[] bytes = await client.GetByteArrayAsync("./content/assets.zip");
            using MemoryStream stream = new(bytes);
            using ZipArchive archive = new(stream, ZipArchiveMode.Read);
            const string root = "/mooh";
            int done = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string path = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!path.StartsWith(root + "/", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Asset path leaves the game directory.");
                }
                _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using Stream source = entry.Open();
                using FileStream destination = File.Create(path);
                await source.CopyToAsync(destination);
                PageInterop.ReportProgress("assets", ++done, archive.Entries.Count);
            }
            return root + "/assets";
        }
    }
}
