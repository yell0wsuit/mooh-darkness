using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

using Mooh.Presentation.Music;

const int Rate = 44100;
if (args.Length != 2)
{
    throw new ArgumentException("Usage: Mooh.MusicRenderer <assets/music> <output-directory>");
}
string musicDirectory = Path.GetFullPath(args[0]);
string output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
string temporary = Path.Combine(Path.GetTempPath(), "mooh-music-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    using FileStream catalog = File.OpenRead(Path.Combine(musicDirectory, "Music.ini"));
    IReadOnlyDictionary<string, MusicTrack> tracks = MusicCatalog.Load(catalog);
    if (tracks.Values.Select(track => track.File).Distinct().Count() != 1)
    {
        throw new InvalidDataException("This exporter expects the original single-module music catalog.");
    }
    byte[] encoded = File.ReadAllBytes(Path.Combine(musicDirectory, tracks.Values.First().File + ".skm"));
    SkaleModule module = SkaleModule.Load(encoded);
    foreach (SkaleSample sample in module.Instruments.Values.SelectMany(instrument => instrument.Samples.Values))
    {
        if (sample.Ogg.Length == 0)
        {
            continue;
        }
        string ogg = Path.Combine(temporary, "sample.ogg");
        string pcm = Path.Combine(temporary, "sample.f32");
        File.WriteAllBytes(ogg, sample.Ogg);
        Ffmpeg("-i", ogg, "-ac", "1", "-ar", "44100", "-f", "f32le", pcm);
        byte[] bytes = File.ReadAllBytes(pcm);
        sample.SetDecoded(MemoryMarshal.Cast<byte, float>(bytes).ToArray());
    }
    Dictionary<string, object> entries = [];
    foreach (MusicTrack track in tracks.Values)
    {
        Dictionary<int, long> orders = [];
        long? loopEnd = null;
        long loopStart = 0;
        int? loopOrder = null;
        List<long> boundaries = [];
        SkalePlayer player = new(module, Rate, track.Position);
        player.OrderStarted += (order, frame) =>
        {
            if (loopOrder == order)
            {
                boundaries.Add(frame);
            }
            else if (!orders.TryAdd(order, frame) && loopOrder is null)
            {
                loopOrder = order;
                boundaries.Add(frame);
            }
        };
        string rendered = Path.Combine(temporary, "music.f32");
        long written = 0;
        float[] chunk = new float[4096 * 2];
        const int VerifyFrames = 1024;
        byte[] previous = new byte[VerifyFrames * 2 * sizeof(float)];
        byte[] following = new byte[previous.Length];
        int checkedBoundaries = 0;
        using (FileStream stream = File.Create(rendered))
        {
            while (loopEnd is null)
            {
                if (written >= Rate * 60 * 20)
                {
                    throw new InvalidDataException("Music did not settle into a repeatable loop within twenty minutes.");
                }
                player.Render(chunk);
                stream.Write(MemoryMarshal.AsBytes(chunk.AsSpan()));
                written += chunk.Length / 2;
                if (boundaries.Count < 2 || checkedBoundaries == boundaries.Count || written < boundaries[^1] + VerifyFrames)
                {
                    continue;
                }
                // Preserve the cold start, then loop a settled cycle. Sustained
                // notes/effect memory must match the following continuous cycle.
                stream.Flush();
                stream.Position = boundaries[^2] * 2 * sizeof(float);
                stream.ReadExactly(previous);
                stream.Position = boundaries[^1] * 2 * sizeof(float);
                stream.ReadExactly(following);
                stream.Position = stream.Length;
                checkedBoundaries = boundaries.Count;
                if (previous.AsSpan().SequenceEqual(following))
                {
                    loopStart = boundaries[^2];
                    loopEnd = boundaries[^1];
                    written = loopEnd.Value;
                    stream.SetLength(written * 2 * sizeof(float));
                }
            }
        }
        string file = track.Name + ".ogg";
        Ffmpeg("-f", "f32le", "-ar", "44100", "-ac", "2", "-i", rendered, "-map_metadata", "-1", "-c:a", "libopus", "-b:a", "192k", "-vbr", "on", "-ar", "48000", "-fflags", "+bitexact", "-flags:a", "+bitexact", Path.Combine(output, file));
        entries.Add(track.Name, new
        {
            file,
            offset = 0,
            loopStart = loopStart / (double)Rate,
            loopEnd = written / (double)Rate,
            volume = track.Volume,
        });
        Console.WriteLine($"Rendered {track.Name}: {written / (double)Rate:F2}s, loop starts at {loopStart / (double)Rate:F2}s");
    }
    byte[] source = [.. encoded, .. File.ReadAllBytes(Path.Combine(musicDirectory, "Music.ini"))];
    File.WriteAllText(Path.Combine(output, "tracks.json"), JsonSerializer.Serialize(new
    {
        formatVersion = 4,
        codec = "opus",
        bitRate = 192000,
        sampleRate = 48000,
        renderSampleRate = Rate,
        sourceSha256 = Convert.ToHexString(SHA256.HashData(source)),
        tracks = entries,
    }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
}
finally
{
    Directory.Delete(temporary, true);
}

static void Ffmpeg(params string[] arguments)
{
    ProcessStartInfo info = new("ffmpeg")
    {
        UseShellExecute = false,
    };
    info.ArgumentList.Add("-hide_banner");
    info.ArgumentList.Add("-loglevel");
    info.ArgumentList.Add("error");
    info.ArgumentList.Add("-y");
    foreach (string argument in arguments)
    {
        info.ArgumentList.Add(argument);
    }
    using Process process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start ffmpeg");
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"ffmpeg failed with exit code {process.ExitCode}");
    }
}
