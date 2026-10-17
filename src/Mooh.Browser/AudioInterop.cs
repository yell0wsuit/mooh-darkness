using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace Mooh.Browser
{
    internal static partial class AudioInterop
    {
        internal static Task ImportAsync()
        {
            return JSHost.ImportAsync("audio", "../audio.js");
        }

        [JSImport("decode", "audio")]
        internal static partial Task<int> Decode(string key, string base64);

        [JSImport("preloadMusic", "audio")]
        internal static partial Task PreloadMusic();

        [JSImport("music", "audio")]
        internal static partial void Music(string track, double volume);

        [JSImport("play", "audio")]
        internal static partial int Play(string key, double left, double right, double rate);

        [JSImport("playing", "audio")]
        internal static partial bool Playing(int voice);

        [JSImport("suspended", "audio")]
        internal static partial void Suspended(bool suspended);
    }
}
