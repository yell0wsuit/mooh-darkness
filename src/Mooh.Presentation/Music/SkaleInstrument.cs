using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Mooh.Presentation.Music
{
    /// <summary>An instrument sample with its external Ogg payload.</summary>
    public sealed class SkaleSample
    {
        /// <summary>No loop.</summary>
        public const int NoLoop = 0;

        /// <summary>A forward loop.</summary>
        public const int ForwardLoop = 1;

        /// <summary>A ping-pong loop.</summary>
        public const int PingPongLoop = 2;

        /// <summary>Gets the sample number within its instrument.</summary>
        public int Index
        {
            get; internal set;
        }

        /// <summary>Gets the default volume, 0 to 128.</summary>
        public int Volume { get; internal set; } = 128;

        /// <summary>Gets the panning, 0 to 255.</summary>
        public int Panning { get; internal set; } = 128;

        /// <summary>Gets the finetune.</summary>
        public int Finetune
        {
            get; internal set;
        }

        /// <summary>Gets the relative note.</summary>
        public int RelativeNote
        {
            get; internal set;
        }

        /// <summary>Gets the loop start in frames.</summary>
        public int LoopStart
        {
            get; internal set;
        }

        /// <summary>Gets the loop end in frames.</summary>
        public int LoopEnd
        {
            get; internal set;
        }

        /// <summary>Gets the loop type.</summary>
        public int LoopType
        {
            get; internal set;
        }

        /// <summary>
        /// Gets the sample's length in frames, which the payload's header declares; a decoder that
        /// pads or trims its output is fitted to it.
        /// </summary>
        public int Length
        {
            get; internal set;
        }

        /// <summary>Gets the embedded Ogg Vorbis payload.</summary>
        public byte[] Ogg { get; internal set; } = [];

        /// <summary>Gives the sample decoded mono samples, fitted to its declared length.</summary>
        /// <param name="decoded">The decoded samples.</param>
        public void SetDecoded(float[] decoded)
        {
            ArgumentNullException.ThrowIfNull(decoded);
            if (Length <= 0 || decoded.Length == Length)
            {
                Pcm = decoded;
                return;
            }

            float[] fitted = new float[Length];
            decoded.AsSpan(0, Math.Min(decoded.Length, Length)).CopyTo(fitted);
            Pcm = fitted;
        }

        /// <summary>Gets or sets the decoded mono samples.</summary>
        public float[]? Pcm
        {
            get; set;
        }

        /// <summary>Gets whether the sample can play.</summary>
        public bool IsUsable => Pcm is { Length: > 1 };
    }

    /// <summary>An instrument envelope stored in a version 3 subchunk. Points are (x, y), y in 0 to 64.</summary>
    public sealed class SkaleEnvelope
    {
        /// <summary>Gets the kind: 1 volume, 2 panning, 3 filter, 4 resonance.</summary>
        public int Kind
        {
            get; internal set;
        }

        /// <summary>Gets the slot within its kind.</summary>
        public int Slot
        {
            get; internal set;
        }

        /// <summary>Gets whether the envelope is on.</summary>
        public bool Enabled
        {
            get; internal set;
        }

        /// <summary>Gets the points.</summary>
        public List<(int X, int Y)> Points { get; } = [];

        /// <summary>Gets whether a sustain point holds while the key is down.</summary>
        public bool SustainOn
        {
            get; internal set;
        }

        /// <summary>Gets the sustain point.</summary>
        public int SustainPoint
        {
            get; internal set;
        }

        /// <summary>Gets whether the envelope loops.</summary>
        public bool LoopOn
        {
            get; internal set;
        }

        /// <summary>Gets the loop's first point.</summary>
        public int LoopStart
        {
            get; internal set;
        }

        /// <summary>Gets the loop's last point.</summary>
        public int LoopEnd
        {
            get; internal set;
        }

        /// <summary>Gets whether the envelope advances by <c>125 / bpm</c> per tick rather than 1.</summary>
        public bool TempoScaled
        {
            get; internal set;
        }
    }

    /// <summary>An instrument with note mappings, samples and envelopes loaded from JSON.</summary>
    public sealed class SkaleInstrument
    {
        private readonly List<(byte[] Samples, int Low, int High)>[] _groups = new List<(byte[], int, int)>[128];
        private readonly Dictionary<int, SkaleSample> _samples = [];
        private readonly Dictionary<(int Kind, int Slot), SkaleEnvelope> _envelopes = [];
        private readonly int[] _envelopeSelection = new int[4];

        private SkaleInstrument()
        {
            for (int note = 0; note < 128; note++)
            {
                _groups[note] = [];
            }
        }

        /// <summary>Gets the zero-based instrument number.</summary>
        public int Index
        {
            get; private set;
        }

        /// <summary>Gets the notes that play at 60 whatever the cell says.</summary>
        public bool[] FixedNote { get; } = new bool[128];

        /// <summary>Gets the samples by number.</summary>
        public IReadOnlyDictionary<int, SkaleSample> Samples => _samples;

        /// <summary>Gets the fadeout per tick after key off.</summary>
        public int Fadeout
        {
            get; private set;
        }

        /// <summary>Gets how a new note treats the previous voice: zero stops it, one releases its envelope.</summary>
        public int NewNoteAction
        {
            get; private set;
        }

        /// <summary>Gets the instrument's gain.</summary>
        public float Volume { get; private set; } = 1;

        /// <summary>Gets the instrument's panning, 0 to 1.</summary>
        public float Panning { get; private set; } = 0.5f;

        /// <summary>Gets the transpose.</summary>
        public int Transpose
        {
            get; private set;
        }

        /// <summary>Gets the finetune.</summary>
        public int Finetune
        {
            get; private set;
        }

        /// <summary>Gets the per-instrument effect remap.</summary>
        public int[] EffectMap { get; } = new int[21];

        /// <summary>Gets the selected envelope of a kind.</summary>
        /// <param name="kind">1 volume, 2 panning.</param>
        /// <returns>The envelope, or <see langword="null"/>.</returns>
        public SkaleEnvelope? Envelope(int kind)
        {
            return _envelopes.GetValueOrDefault((kind, _envelopeSelection[kind - 1]));
        }

        /// <summary>Picks the sample a note plays at a velocity.</summary>
        /// <param name="note">The note.</param>
        /// <param name="velocity">The velocity.</param>
        /// <returns>The sample, or <see langword="null"/>.</returns>
        public SkaleSample? Select(int note, int velocity)
        {
            if (note is < 0 or >= 128)
            {
                return null;
            }

            foreach ((byte[] samples, int low, int high) in _groups[note])
            {
                if (low <= velocity && velocity <= high)
                {
                    return samples.Length > 0 ? _samples.GetValueOrDefault(samples[0]) : null;
                }
            }

            return null;
        }

        internal static SkaleInstrument Read(JsonElement data, Func<string, byte[]>? loadSample)
        {
            SkaleInstrument instrument = new()
            {
                Index = data.GetProperty("index").GetInt32(),
                Fadeout = data.GetProperty("fadeout").GetInt32(),
                NewNoteAction = data.GetProperty("newNoteAction").GetInt32(),
                Volume = data.GetProperty("volume").GetSingle(),
                Panning = data.GetProperty("panning").GetSingle(),
                Transpose = data.GetProperty("transpose").GetInt32(),
                Finetune = data.GetProperty("finetune").GetInt32(),
            };
            if (instrument.Index is < 0 or > 255 || !float.IsFinite(instrument.Volume) || !float.IsFinite(instrument.Panning))
            {
                throw new InvalidDataException("Invalid tracker instrument index, gain or panning.");
            }
            JsonElement fixedNote = data.GetProperty("fixedNote"), groups = data.GetProperty("groups"),
                selection = data.GetProperty("envelopeSelection"), effects = data.GetProperty("effectMap");
            if (fixedNote.GetArrayLength() != 128 || groups.GetArrayLength() != 128
                || selection.GetArrayLength() != 4 || effects.GetArrayLength() != 21)
            {
                throw new InvalidDataException("Tracker instrument mappings have incorrect lengths.");
            }
            for (int note = 0; note < 128; note++)
            {
                instrument.FixedNote[note] = fixedNote[note].GetBoolean();
                foreach (JsonElement group in groups[note].EnumerateArray())
                {
                    byte[] samples = [.. group.GetProperty("samples").EnumerateArray().Select(sample => sample.GetByte())];
                    int low = group.GetProperty("low").GetInt32(), high = group.GetProperty("high").GetInt32();
                    if (low < 0 || high < low || high > 255)
                    {
                        throw new InvalidDataException("Invalid sample velocity range.");
                    }
                    instrument._groups[note].Add((samples, low, high));
                }
            }
            for (int kind = 0; kind < 4; kind++)
            {
                instrument._envelopeSelection[kind] = selection[kind].GetInt32();
            }
            for (int effect = 0; effect < 21; effect++)
            {
                instrument.EffectMap[effect] = effects[effect].GetInt32();
            }
            foreach (JsonElement item in data.GetProperty("envelopes").EnumerateArray())
            {
                SkaleEnvelope envelope = new()
                {
                    Kind = item.GetProperty("kind").GetInt32(),
                    Slot = item.GetProperty("slot").GetInt32(),
                    Enabled = item.GetProperty("enabled").GetBoolean(),
                    SustainOn = item.GetProperty("sustainOn").GetBoolean(),
                    SustainPoint = item.GetProperty("sustainPoint").GetInt32(),
                    LoopOn = item.GetProperty("loopOn").GetBoolean(),
                    LoopStart = item.GetProperty("loopStart").GetInt32(),
                    LoopEnd = item.GetProperty("loopEnd").GetInt32(),
                    TempoScaled = item.GetProperty("tempoScaled").GetBoolean(),
                };
                foreach (JsonElement point in item.GetProperty("points").EnumerateArray())
                {
                    envelope.Points.Add((point.GetProperty("x").GetInt32(), point.GetProperty("y").GetInt32()));
                }
                if (envelope.Kind is < 1 or > 4 || !instrument._envelopes.TryAdd((envelope.Kind, envelope.Slot), envelope))
                {
                    throw new InvalidDataException("Invalid or duplicated instrument envelope.");
                }
            }
            foreach (JsonElement item in data.GetProperty("samples").EnumerateArray())
            {
                SkaleSample sample = new()
                {
                    Index = item.GetProperty("index").GetInt32(),
                    Volume = item.GetProperty("volume").GetInt32(),
                    Panning = item.GetProperty("panning").GetInt32(),
                    Finetune = item.GetProperty("finetune").GetInt32(),
                    RelativeNote = item.GetProperty("relativeNote").GetInt32(),
                    LoopStart = item.GetProperty("loopStart").GetInt32(),
                    LoopEnd = item.GetProperty("loopEnd").GetInt32(),
                    LoopType = item.GetProperty("loopType").GetInt32(),
                    Length = item.GetProperty("length").GetInt32(),
                };
                string? file = item.GetProperty("file").GetString();
                sample.Ogg = file is null ? [] : (loadSample ?? throw new InvalidDataException("External samples require a file resolver."))(file);
                if (sample.Length < 0 || sample.LoopStart < 0 || sample.LoopEnd < sample.LoopStart
                    || sample.LoopType is < 0 or > 2 || !instrument._samples.TryAdd(sample.Index, sample))
                {
                    throw new InvalidDataException("Invalid or duplicated tracker sample.");
                }
            }
            return instrument;
        }
    }
}
