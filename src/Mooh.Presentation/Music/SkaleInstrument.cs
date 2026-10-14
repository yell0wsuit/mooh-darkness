using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Mooh.Presentation.Music
{
    /// <summary>A sample of an instrument, <c>sub_10014C50</c>, with its Ogg payload.</summary>
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

    /// <summary>An instrument envelope, subchunk v3 (<c>sub_10013F50</c>). Points are (x, y), y in 0 to 64.</summary>
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

    /// <summary>An instrument, version 6 (<c>sub_100179A0</c>).</summary>
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

        /// <summary>Gets the notes that play at 60 whatever the cell says, <c>inst+3592</c>.</summary>
        public bool[] FixedNote { get; } = new bool[128];

        /// <summary>Gets the samples by number.</summary>
        public IReadOnlyDictionary<int, SkaleSample> Samples => _samples;

        /// <summary>Gets the fadeout per tick after key off, <c>inst+3720</c>.</summary>
        public int Fadeout
        {
            get; private set;
        }

        /// <summary>Gets the instrument's gain, <c>inst+20552</c>.</summary>
        public float Volume { get; private set; } = 1;

        /// <summary>Gets the instrument's panning, 0 to 1, <c>inst+4064</c>.</summary>
        public float Panning { get; private set; } = 0.5f;

        /// <summary>Gets the transpose, <c>inst+4068</c>.</summary>
        public int Transpose
        {
            get; private set;
        }

        /// <summary>Gets the finetune, <c>inst+4072</c>.</summary>
        public int Finetune
        {
            get; private set;
        }

        /// <summary>Gets the per-instrument effect remap, <c>inst+3728</c>.</summary>
        public int[] EffectMap { get; } = new int[21];

        /// <summary>Gets the selected envelope of a kind.</summary>
        /// <param name="kind">1 volume, 2 panning.</param>
        /// <returns>The envelope, or <see langword="null"/>.</returns>
        public SkaleEnvelope? Envelope(int kind)
        {
            return _envelopes.GetValueOrDefault((kind, _envelopeSelection[kind - 1]));
        }

        /// <summary>Picks the sample a note plays at a velocity, <c>sub_10007240</c>.</summary>
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

        internal static SkaleInstrument Read(byte[] data, int start, int end)
        {
            SkaleModule.Reader reader = new(data, start);
            SkaleInstrument instrument = new()
            {
                Index = reader.U8(),
            };
            _ = reader.CString();
            for (int note = 0; note < 128; note++)
            {
                int groups = reader.U8();
                for (int group = 0; group < groups; group++)
                {
                    int count = reader.U8();
                    byte[] samples = reader.Raw(count);
                    int low = reader.U8();
                    int high = reader.U8();
                    instrument._groups[note].Add((samples, low, high));
                }

                instrument.FixedNote[note] = reader.U8() != 0;
            }

            ReadOnlySpan<byte> block = data.AsSpan(reader.Position, 288);
            reader.Position += 288;
            instrument.Fadeout = BinaryPrimitives.ReadUInt16LittleEndian(block);
            for (int kind = 0; kind < 4; kind++)
            {
                instrument._envelopeSelection[kind] = block[2 + kind];
            }

            instrument.Volume = BinaryPrimitives.ReadSingleLittleEndian(block[9..]);
            instrument.Panning = BinaryPrimitives.ReadSingleLittleEndian(block[13..]) + 0.5f;
            instrument.Transpose = BinaryPrimitives.ReadUInt16LittleEndian(block[17..]);
            instrument.Finetune = BinaryPrimitives.ReadUInt16LittleEndian(block[19..]);
            for (int effect = 0; effect < 21; effect++)
            {
                instrument.EffectMap[effect] = block[25 + effect];
            }

            while (reader.Position + 8 <= end)
            {
                uint id = reader.U32();
                int size = (int)reader.U32();
                int type = (int)(id >> 16);
                if (type == 0xFFFF)
                {
                    break;
                }

                int at = reader.Position;
                if (type == 1)
                {
                    SkaleSample sample = ReadSample(data, at, at + size);
                    instrument._samples[sample.Index] = sample;
                }
                else if (type == 2)
                {
                    SkaleEnvelope envelope = ReadEnvelope(data, at, at + size);
                    if (envelope.Kind is >= 1 and <= 4)
                    {
                        instrument._envelopes[(envelope.Kind, envelope.Slot)] = envelope;
                    }
                }

                reader.Position = at + size;
            }

            return instrument;
        }

        private static SkaleEnvelope ReadEnvelope(byte[] data, int start, int end)
        {
            SkaleModule.Reader reader = new(data, start);
            SkaleEnvelope envelope = new()
            {
                Slot = reader.U8(),
                Kind = reader.U8(),
                Enabled = reader.U8() != 0,
            };
            int count = reader.U8();
            for (int point = 0; point < count; point++)
            {
                int x = reader.U16();
                int y = reader.S16();
                envelope.Points.Add((x, y));
            }

            if (reader.Position + 5 <= end)
            {
                envelope.SustainOn = reader.U8() != 0;
                envelope.SustainPoint = reader.U8();
                envelope.LoopOn = reader.U8() != 0;
                envelope.LoopStart = reader.U8();
                envelope.LoopEnd = reader.U8();
            }

            if (reader.Position < end)
            {
                envelope.TempoScaled = reader.U8() == 1;
            }

            return envelope;
        }

        private static SkaleSample ReadSample(byte[] data, int start, int end)
        {
            SkaleModule.Reader reader = new(data, start);
            SkaleSample sample = new()
            {
                Index = reader.U8(),
            };
            _ = reader.CString();
            sample.Volume = reader.U8();
            sample.Panning = reader.U8();
            sample.Finetune = (sbyte)reader.U8();
            sample.RelativeNote = (sbyte)reader.U8();
            _ = reader.U8();
            sample.LoopStart = (int)reader.U32();
            sample.LoopEnd = (int)reader.U32();
            sample.LoopType = reader.U8();
            while (reader.Position + 8 <= end)
            {
                uint id = reader.U32();
                int size = (int)reader.U32();
                if (id >> 16 == 0xFFFF)
                {
                    break;
                }

                int at = reader.Position;
                if (id >> 16 == 1 && (id & 0xFFFF) == 4)
                {
                    // sub_10013A80: the frame count, a byte, the payload size, the payload.
                    sample.Length = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
                    int payload = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 5));
                    sample.Ogg = data[(at + 9)..(at + 9 + payload)];
                }

                reader.Position = at + size;
            }

            return sample;
        }
    }
}
