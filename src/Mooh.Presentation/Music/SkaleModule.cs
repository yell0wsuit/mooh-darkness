using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mooh.Presentation.Music
{
    /// <summary>
    /// A Skale Tracker module, <c>.skm</c> with the magic <c>ALIM3</c>, as <c>SkalePlayer.dll</c>
    /// loads it. A port of <c>skm_parse.py</c> and the instrument reader of <c>skm_render.py</c>.
    /// </summary>
    /// <remarks>
    /// The file is a header word <c>0xFFFE0000 | version</c>, a 9-byte header block holding the
    /// magic and the tracker version, then a stream of <c>(u32 id, u32 size)</c> chunks ended by
    /// type <c>0xFFFF</c>: 1 Info (v3), 2 Patterns (v1), 3 Instruments (each v6), then MultiFX,
    /// Mixer and Synth. The mixer's leading gain scales the final output.
    /// </remarks>
    public sealed class SkaleModule
    {
        private SkaleModule()
        {
        }

        /// <summary>Gets the title.</summary>
        public string Title { get; private set; } = string.Empty;

        /// <summary>Gets the initial tempo in beats per minute.</summary>
        public int Bpm { get; private set; } = 125;

        /// <summary>Gets the initial ticks per row.</summary>
        public int Speed { get; private set; } = 6;

        /// <summary>Gets the module's final mixer gain, independent of the user's playback volume.</summary>
        public float MasterGain { get; private set; } = 1;

        /// <summary>Gets the order list: pattern numbers by position.</summary>
        public IReadOnlyList<int> Order { get; private set; } = [];

        /// <summary>Gets how many order positions the song uses.</summary>
        public int SongLength
        {
            get; private set;
        }

        /// <summary>Gets the patterns by number.</summary>
        public IReadOnlyDictionary<int, SkalePattern> Patterns { get; private set; } = new Dictionary<int, SkalePattern>();

        /// <summary>Gets the instruments by zero-based number.</summary>
        public IReadOnlyDictionary<int, SkaleInstrument> Instruments { get; private set; } = new Dictionary<int, SkaleInstrument>();

        /// <summary>Gets the number of channels the patterns use.</summary>
        public int ChannelCount
        {
            get; private set;
        }

        /// <summary>Reads a module.</summary>
        /// <param name="data">The file's bytes.</param>
        /// <returns>The module.</returns>
        /// <exception cref="InvalidDataException">The data is not a Skale module.</exception>
        public static SkaleModule Load(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            Reader reader = new(data, 0);
            uint header = reader.U32();
            if ((header & 0xFFFF0000) != 0xFFFE0000 || (header & 0xFFFF) > 1)
            {
                throw new InvalidDataException($"Not a Skale module: header word {header:X8}.");
            }

            _ = reader.U32();
            if (Encoding.ASCII.GetString(reader.Raw(5)) != "ALIM3")
            {
                throw new InvalidDataException("Not a Skale module: the magic is not ALIM3.");
            }

            _ = reader.U32();
            SkaleModule module = new();
            Dictionary<int, SkalePattern> patterns = [];
            Dictionary<int, SkaleInstrument> instruments = [];
            while (reader.Position + 8 <= data.Length)
            {
                uint id = reader.U32();
                int size = (int)reader.U32();
                int type = (int)(id >> 16);
                int version = (int)(id & 0xFFFF);
                if (type == 0xFFFF)
                {
                    break;
                }

                int start = reader.Position;
                if (type == 1 && version == 3)
                {
                    module.ReadInfo(new Reader(data, start));
                }
                else if (type == 2 && version == 1)
                {
                    module.ReadPatterns(new Reader(data, start), start + size, patterns);
                }
                else if (type == 3)
                {
                    ReadInstruments(data, start, start + size, instruments);
                }
                else if (type == 5 && version is 1 or 2)
                {
                    module.MasterGain = BinaryPrimitives.ReadSingleLittleEndian(reader.Raw(4));
                }

                reader.Position = start + size;
            }

            module.Patterns = patterns;
            module.Instruments = instruments;
            int channels = 0;
            foreach (SkalePattern pattern in patterns.Values)
            {
                foreach (int channel in pattern.Channels.Keys)
                {
                    channels = Math.Max(channels, channel + 1);
                }
            }

            module.ChannelCount = Math.Max(channels, 1);
            return module;
        }

        /// <summary>Reads the title, tempo and speed from the version 3 information chunk.</summary>
        private void ReadInfo(Reader reader)
        {
            Title = reader.CString();
            _ = reader.CString();
            _ = reader.CString();
            _ = reader.CString();
            _ = reader.U32();
            _ = reader.U32();
            Bpm = reader.U8();
            Speed = reader.U8();
        }

        /// <summary>Reads the version 1 sequence chunk and its pattern subchunks.</summary>
        private void ReadPatterns(Reader reader, int end, Dictionary<int, SkalePattern> patterns)
        {
            _ = reader.U8();
            byte[] order = reader.Raw(256);
            Order = Array.ConvertAll(order, value => (int)value);
            SongLength = reader.U8();
            _ = reader.U8();
            while (reader.Position + 8 <= end)
            {
                uint id = reader.U32();
                int size = (int)reader.U32();
                if (id >> 16 == 0xFFFF)
                {
                    break;
                }

                int start = reader.Position;
                SkalePattern pattern = ReadPattern(new Reader(reader.Data, start), start + size);
                patterns[pattern.Index] = pattern;
                reader.Position = start + size;
            }
        }

        private static SkalePattern ReadPattern(Reader reader, int end)
        {
            int index = reader.U8();
            int rows = reader.U16();
            SkalePattern pattern = new(index, rows);
            while (reader.Position + 4 <= end)
            {
                int id = reader.U16();
                int size = reader.U16();
                int type = id >> 8;
                int version = id & 0xFF;
                if (type == 0xFF)
                {
                    break;
                }

                if (type == 1 && version == 1)
                {
                    ReadChannel(reader, pattern);
                }
                else
                {
                    reader.Position += size;
                }
            }

            return pattern;
        }

        /// <summary>
        /// Decodes per-channel rows: a byte under <c>0x80</c> is the effect of an
        /// unpacked cell, otherwise a mask of the fields that follow; missing fields keep the
        /// pattern cell's defaults.
        /// </summary>
        private static void ReadChannel(Reader reader, SkalePattern pattern)
        {
            int channel = reader.U8();
            int last = reader.U8() + 1;
            int first = reader.U8();
            SkaleCell[] cells = new SkaleCell[Math.Max(pattern.Rows, last)];
            Array.Fill(cells, SkaleCell.Empty);
            for (int row = first; row < last; row++)
            {
                SkaleCell cell = SkaleCell.Empty;
                int mask = reader.U8();
                if (mask < 0x80)
                {
                    cell = new SkaleCell(reader.U8(), reader.U8(), reader.U16(), mask, 0);
                    cell = cell with
                    {
                        Param = reader.U8()
                    };
                }
                else
                {
                    if ((mask & 0x01) != 0)
                    {
                        cell = cell with
                        {
                            Effect = reader.U8()
                        };
                    }

                    if ((mask & 0x02) != 0)
                    {
                        cell = cell with
                        {
                            Note = reader.U8()
                        };
                    }

                    if ((mask & 0x04) != 0)
                    {
                        cell = cell with
                        {
                            Instrument = reader.U8()
                        };
                    }

                    if ((mask & 0x08) != 0)
                    {
                        cell = cell with
                        {
                            Volume = reader.U16()
                        };
                    }

                    if ((mask & 0x10) != 0)
                    {
                        cell = cell with
                        {
                            Param = reader.U8()
                        };
                    }
                }

                cells[row] = cell;
            }

            pattern.SetChannel(channel, cells);
        }

        /// <summary>Reads version 6 instruments from chunk 3.</summary>
        private static void ReadInstruments(byte[] data, int start, int end, Dictionary<int, SkaleInstrument> instruments)
        {
            Reader reader = new(data, start);
            while (reader.Position + 8 <= end)
            {
                uint id = reader.U32();
                int size = (int)reader.U32();
                if (id >> 16 == 0xFFFF)
                {
                    break;
                }

                int at = reader.Position;
                if ((id & 0xFFFF) == 6)
                {
                    SkaleInstrument instrument = SkaleInstrument.Read(data, at, at + size);
                    instruments[instrument.Index] = instrument;
                }

                reader.Position = at + size;
            }
        }

        /// <summary>A sequential little-endian reader, mirroring the DLL's stream primitives.</summary>
        internal sealed class Reader(byte[] data, int position)
        {
            public byte[] Data { get; } = data;

            public int Position { get; set; } = position;

            public int U8()
            {
                return Data[Position++];
            }

            public int U16()
            {
                int value = BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(Position));
                Position += 2;
                return value;
            }

            public short S16()
            {
                short value = BinaryPrimitives.ReadInt16LittleEndian(Data.AsSpan(Position));
                Position += 2;
                return value;
            }

            public uint U32()
            {
                uint value = BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(Position));
                Position += 4;
                return value;
            }

            public byte[] Raw(int count)
            {
                byte[] value = Data[Position..(Position + count)];
                Position += count;
                return value;
            }

            /// <summary>Reads a NUL-terminated Latin-1 string.</summary>
            public string CString()
            {
                int end = Array.IndexOf(Data, (byte)0, Position);
                string value = Encoding.Latin1.GetString(Data, Position, end - Position);
                Position = end + 1;
                return value;
            }
        }
    }
}
