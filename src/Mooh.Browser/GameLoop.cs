using System;
using System.Runtime.InteropServices;

namespace Mooh.Browser
{
    /// <summary>Drains the donor host's event ring and draws on its WebGL-owning thread.</summary>
    internal static unsafe class GameLoop
    {
        private static BrowserGame? _game;
        private static bool _active = true;
        private static int _cssWidth;
        private static int _cssHeight;
        internal static SkiaSurface Surface { get; set; } = null!;
        internal static Func<BrowserGame> CreateGame { get; set; } = null!;

        internal static void Start(int cssWidth, int cssHeight)
        {
            _cssWidth = cssWidth;
            _cssHeight = cssHeight;
            HostShim.SetFrameCallback(&OnFrame);
            HostShim.RequestFrame();
        }

        [UnmanagedCallersOnly]
        private static void OnFrame(double timestamp)
        {
            try
            {
                if (HostShim.ContextLost() != 0)
                {
                    _game?.SetActive(false);
                    return;
                }
                for (int pass = 0; pass < 8; pass++)
                {
                    ReadOnlySpan<HostEvent> batch = HostEventQueue.Drain();
                    if (batch.IsEmpty)
                    {
                        break;
                    }
                    foreach (HostEvent input in batch)
                    {
                        Handle(input);
                    }
                }
                _game?.Frame(timestamp, Surface);
                HostShim.RequestFrame();
            }
            catch (Exception failure)
            {
                _game?.SetActive(false);
                Console.Error.WriteLine(failure);
                PageInterop.Fail(failure.Message);
            }
        }

        private static void Handle(in HostEvent input)
        {
            switch (input.Kind)
            {
                case HostEventKind.Start:
                    if (_game is null)
                    {
                        _game = CreateGame();
                        _game.Resize(_cssWidth, _cssHeight);
                        _game.SetActive(_active);
                    }
                    break;
                case HostEventKind.Active:
                    _active = input.Word0 != 0;
                    _game?.SetActive(_active);
                    break;
                case HostEventKind.Resize:
                    _cssWidth = Math.Max(1, (int)BitConverter.Int32BitsToSingle(input.Word0));
                    _cssHeight = Math.Max(1, (int)BitConverter.Int32BitsToSingle(input.Word1));
                    float ratio = Math.Clamp(BitConverter.Int32BitsToSingle(input.Word2), 1, 2);
                    int width = Math.Max(1, (int)Math.Round(_cssWidth * ratio));
                    int height = Math.Max(1, (int)Math.Round(_cssHeight * ratio));
                    _ = HostShim.ResizeCanvas(width, height);
                    Surface.Resize(width, height);
                    _game?.Resize(_cssWidth, _cssHeight);
                    break;
                case HostEventKind.Pointer:
                    _game?.Pointer(input);
                    break;
                case HostEventKind.Key:
                    _game?.Key(input);
                    break;
                case HostEventKind.Text:
                    _game?.Text(input.Word0);
                    break;
                case HostEventKind.None:
                case HostEventKind.Wheel:
                default:
                    break;
            }
        }
    }
}
