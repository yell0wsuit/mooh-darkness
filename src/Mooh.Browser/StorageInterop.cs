using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace Mooh.Browser
{
    internal static partial class StorageInterop
    {
        public static Task ImportAsync()
        {
            return JSHost.ImportAsync("storage", "../storage.js");
        }
        [JSImport("read", "storage")]
        public static partial string? Read(string key);
        [JSImport("write", "storage")]
        public static partial void Write(string key, string value);
    }
}
