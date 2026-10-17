using System;
using System.IO;
using System.Threading.Tasks;

using Mooh.Desktop.Platform.Audio;

namespace Mooh.Browser
{
    /// <summary>Web Audio effects; browser music uses an offline-rendered recording.</summary>
    internal sealed class WebAudioOutput : IAudioOutput
    {
        public static async Task PreloadAsync(string assets)
        {
            foreach (string path in Directory.EnumerateFiles(Path.Combine(assets, "sounds"), "*.ogg"))
            {
                _ = await AudioInterop.Decode(path, Convert.ToBase64String(File.ReadAllBytes(path)));
            }
            await AudioInterop.PreloadMusic();
        }
        public object? Load(string path)
        {
            return File.Exists(path) ? path : null;
        }
        public IVoice Play(object sound, float left, float right, float rate)
        {
            return new Voice(AudioInterop.Play((string)sound, left, right, rate));
        }
        public (float[] Samples, int Rate)? DecodeMono(byte[] encoded)
        {
            return null;
        }
        public IAudioFeed? OpenFeed(int rate)
        {
            return null;
        }
        public void Dispose()
        {
            AudioInterop.Music(string.Empty, 0);
            AudioInterop.Suspended(true);
        }

        private sealed class Voice(int id) : IVoice
        {
            public bool IsPlaying => AudioInterop.Playing(id);
        }
    }
}
