using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using SDL3;

namespace Mooh.Desktop.Platform.Audio
{
    /// <summary>
    /// SDL_mixer playback, after contre-jour-dx's <c>SdlAudioBackend</c>: effects are decoded once
    /// and played on a pool of reused tracks, each with its own stereo gains and rate, which is how
    /// the original's <c>ObjSoundPlay</c> pans and pitches a sound.
    /// </summary>
    public sealed class SdlAudio : IAudioOutput
    {
        private const int MixerFrequency = 44100;
        private const int MixerChannels = 2;

        private readonly nint _mixer;
        private readonly List<nint> _loaded = [];
        private readonly List<Track> _tracks = [];
        private bool _disposed;

        private SdlAudio(nint mixer)
        {
            _mixer = mixer;
        }

        /// <summary>
        /// Opens the default playback device. A machine without one still plays the game silently,
        /// so a failure is reported through <paramref name="error"/> rather than thrown.
        /// </summary>
        /// <param name="error">Why audio is unavailable, when it is.</param>
        /// <returns>The audio, or <see langword="null"/>.</returns>
        public static SdlAudio? TryOpen(out string? error)
        {
            bool started = false;
            try
            {
                if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
                {
                    error = $"could not start SDL audio: {SDL.GetError()}";
                    return null;
                }

                started = true;
                if (!Mixer.Init())
                {
                    error = $"could not start SDL_mixer: {SDL.GetError()}";
                    SDL.QuitSubSystem(SDL.InitFlags.Audio);
                    return null;
                }

                nint mixer = CreateDeviceMixer();
                if (mixer == 0)
                {
                    error = $"could not open the default playback device: {SDL.GetError()}";
                    Mixer.Quit();
                    SDL.QuitSubSystem(SDL.InitFlags.Audio);
                    return null;
                }

                error = null;
                return new SdlAudio(mixer);
            }
            catch (Exception failure) when (failure is DllNotFoundException or EntryPointNotFoundException)
            {
                error = $"could not load the SDL audio libraries: {failure.Message.Split('\n')[0]}";
                if (started)
                {
                    SDL.QuitSubSystem(SDL.InitFlags.Audio);
                }

                return null;
            }
        }

        /// <inheritdoc/>
        public object? Load(string path)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!File.Exists(path))
            {
                return null;
            }

            nint audio = Mixer.LoadAudio(_mixer, path, true);
            if (audio == 0)
            {
                return null;
            }

            _loaded.Add(audio);
            return new Sound(audio);
        }

        /// <inheritdoc/>
        public IVoice Play(object sound, float left, float right, float rate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(sound);
            Track track = IdleTrack();
            _ = Mixer.SetTrackAudio(track.Handle, ((Sound)sound).Audio);
            SetStereo(track.Handle, left, right);
            _ = Mixer.SetTrackFrequencyRatio(track.Handle, rate);
            _ = Mixer.PlayTrack(track.Handle, 0);
            return track;
        }

        /// <inheritdoc/>
        public (float[] Samples, int Rate)? DecodeMono(byte[] encoded)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(encoded);
            if (encoded.Length == 0)
            {
                return null;
            }

            fixed (byte* bytes = encoded)
            {
                nint io = SDL.IOFromConstMem((nint)bytes, (nuint)encoded.Length);
                if (io == 0)
                {
                    return null;
                }

                nint decoder = Mixer.CreateAudioDecoderIO(io, true, 0);
                if (decoder == 0)
                {
                    return null;
                }

                try
                {
                    SDL.AudioSpec source = default;
                    if (!Mixer.GetAudioDecoderFormat(decoder, (nint)(&source)))
                    {
                        return null;
                    }

                    SDL.AudioSpec mono = new()
                    {
                        Format = SDL.AudioFormat.AudioF32LE,
                        Channels = 1,
                        Freq = source.Freq,
                    };
                    List<float> samples = [];
                    float[] chunk = new float[4096];
                    fixed (float* buffer = chunk)
                    {
                        while (true)
                        {
                            int bytesRead = Mixer.DecodeAudio(decoder, (nint)buffer, chunk.Length * sizeof(float), (nint)(&mono));
                            if (bytesRead <= 0)
                            {
                                break;
                            }

                            samples.AddRange(chunk.AsSpan(0, bytesRead / sizeof(float)));
                        }
                    }

                    return ([.. samples], source.Freq);
                }
                finally
                {
                    Mixer.DestroyAudioDecoder(decoder);
                }
            }
        }

        /// <inheritdoc/>
        public IAudioFeed? OpenFeed(int rate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SDL.AudioSpec spec = new()
            {
                Format = SDL.AudioFormat.AudioF32LE,
                Channels = MixerChannels,
                Freq = rate,
            };
            nint stream = SDL.CreateAudioStream(in spec, in spec);
            if (stream == 0)
            {
                return null;
            }

            nint track = Mixer.CreateTrack(_mixer);
            if (track == 0)
            {
                SDL.DestroyAudioStream(stream);
                return null;
            }

            _ = Mixer.SetTrackAudioStream(track, stream);
            _ = Mixer.PlayTrack(track, 0);
            return new Feed(track, stream);
        }

        /// <summary>Releases the tracks, then the sounds, then the mixer that owns both.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (Track track in _tracks)
            {
                Mixer.DestroyTrack(track.Handle);
            }

            _tracks.Clear();
            foreach (nint audio in _loaded)
            {
                Mixer.DestroyAudio(audio);
            }

            _loaded.Clear();
            Mixer.DestroyMixer(_mixer);
            Mixer.Quit();
            SDL.QuitSubSystem(SDL.InitFlags.Audio);
        }

        private static nint CreateDeviceMixer()
        {
            SDL.AudioSpec spec = new()
            {
                Format = SDL.AudioFormat.AudioF32LE,
                Channels = MixerChannels,
                Freq = MixerFrequency,
            };
            nint specPointer = Marshal.AllocHGlobal(Marshal.SizeOf<SDL.AudioSpec>());
            try
            {
                Marshal.StructureToPtr(spec, specPointer, false);
                return Mixer.CreateMixerDevice(SDL.AudioDeviceDefaultPlayback, specPointer);
            }
            finally
            {
                Marshal.FreeHGlobal(specPointer);
            }
        }

        private static void SetStereo(nint track, float left, float right)
        {
            Mixer.StereoGains gains = new()
            {
                Left = left,
                Right = right,
            };
            nint pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Mixer.StereoGains>());
            try
            {
                Marshal.StructureToPtr(gains, pointer, false);
                _ = Mixer.SetTrackStereo(track, pointer);
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        /// <summary>A finished track is reused; the pool only grows to the most sounds heard at once.</summary>
        private Track IdleTrack()
        {
            foreach (Track track in _tracks)
            {
                if (!track.IsPlaying)
                {
                    return track;
                }
            }

            nint handle = Mixer.CreateTrack(_mixer);
            if (handle == 0)
            {
                throw new InvalidOperationException($"Could not create an audio track: {SDL.GetError()}");
            }

            Track created = new(handle);
            _tracks.Add(created);
            return created;
        }

        private sealed class Feed(nint track, nint stream) : IAudioFeed
        {
            private bool _disposed;

            public int QueuedFrames => _disposed ? 0 : SDL.GetAudioStreamQueued(stream) / (MixerChannels * sizeof(float));

            public float Volume
            {
                set => _ = Mixer.SetTrackGain(track, value);
            }

            public void Write(ReadOnlySpan<float> interleaved)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(interleaved);
                _ = SDL.PutAudioStreamData(stream, bytes, bytes.Length);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Mixer.DestroyTrack(track);
                SDL.DestroyAudioStream(stream);
            }
        }

        private sealed class Sound(nint audio)
        {
            public nint Audio { get; } = audio;
        }

        private sealed class Track(nint handle) : IVoice
        {
            public nint Handle { get; } = handle;

            public bool IsPlaying => Mixer.TrackPlaying(Handle) || Mixer.TrackPaused(Handle);
        }
    }
}
