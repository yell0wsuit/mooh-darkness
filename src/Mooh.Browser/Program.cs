using System;
using System.Runtime.Versioning;
#if WASM_THREADS
using System.Threading.Tasks;
#endif

using Mooh.Browser;

[assembly: SupportedOSPlatform("browser")]

await PageInterop.ImportAsync();
await StorageInterop.ImportAsync();
await HostEventInterop.ImportAsync();
await AudioInterop.ImportAsync();
#if WASM_THREADS
if (HostShim.IsMainRuntimeThread() != 0)
{
    throw new InvalidOperationException("The threaded game must own a worker.");
}
if (HostShim.AcquireCanvas() == 0)
{
    throw new InvalidOperationException("Cannot prepare canvas transfer.");
}
int[] size = PageInterop.TransferCanvasToThread("game", HostShim.ThreadId());
if (size.Length != 4)
{
    throw new InvalidOperationException("Cannot transfer the game canvas.");
}
while (HostShim.CanvasReceived() == 0)
{
    await Task.Delay(1);
}
#else
int[] size = PageInterop.MeasureCanvas("game");
if (size.Length != 4 || HostShim.AcquireCanvas() == 0)
{
    throw new InvalidOperationException("Cannot acquire the game canvas.");
}
#endif
if (HostShim.CreateContext(size[2], size[3]) == 0)
{
    throw new InvalidOperationException("WebGL2 is unavailable.");
}
SkiaSurface surface = new(0, size[2], size[3]);
string assets = await ContentPreloader.LoadAsync();
WebAudioOutput audio = new();
await WebAudioOutput.PreloadAsync(assets);
GameLoop.Surface = surface;
GameLoop.CreateGame = () => new BrowserGame(assets, audio);
HostEventQueue.Initialize();
PageInterop.WatchCanvas("game");
GameLoop.Start(size[0], size[1]);
PageInterop.Ready();
Console.WriteLine("Mooh browser boot complete | " + typeof(BrowserGame).Assembly.GetName().Version);
