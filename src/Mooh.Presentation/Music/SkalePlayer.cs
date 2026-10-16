using System;
using System.Collections.Generic;

namespace Mooh.Presentation.Music
{
    /// <summary>
    /// Plays a Skale module the way <c>SkalePlayer.dll</c> does: a port of the DLL-verified replay
    /// engine in <c>skm_render.py</c>, rendering stereo frames on demand so a host can stream it.
    /// </summary>
    /// <remarks>
    /// Timing is <c>rate * 2.5 / bpm</c> frames a tick; pitch is
    /// <c>8363 * 2^((4608 - period) / 768)</c> with <c>period = 7680 - 64 * note - finetune / 2</c>.
    /// A channel's volume is <c>fade * volume * envelope / 0x400000</c>, panned and scaled by
    /// the instrument. The MultiFX and Mixer chunks are not applied, as in the Python port.
    /// The sequence plays on past the song's end from its first position, so a host can loop it.
    /// </remarks>
    public sealed class SkalePlayer
    {
        private const double PitchReferenceHz = 8363;
        private const int NoteOff = 129;
        private const int NoNote = 128;
        private const double MaxPeriod = 7680;
        private const int FadeFull = 0x10000;
        private const double MixScale = 0.003 / 128;

        /// <summary><c>byte_1004F0F4</c>: the 32-entry half sine both vibrato and tremolo use.</summary>
        private static readonly int[] VibratoTable =
        [
            0x00, 0x18, 0x31, 0x4A, 0x61, 0x78, 0x8D, 0xA1, 0xB4, 0xC5, 0xD4, 0xE0, 0xEB, 0xF4, 0xFA, 0xFD,
            0xFF, 0xFD, 0xFA, 0xF4, 0xEB, 0xE0, 0xD4, 0xC5, 0xB4, 0xA1, 0x8D, 0x78, 0x61, 0x4A, 0x31, 0x18,
        ];

        private readonly SkaleModule _module;
        private readonly int _rate;
        private readonly Channel[] _channels;
        private readonly int[] _loopRow;
        private readonly int[] _loopCount;
        private float[] _tick = [];
        private int _tickFrames;
        private int _tickOffset;
        private int _speed;
        private int _bpm;
        private int _order;
        private SkalePattern? _pattern;
        private int _row;
        private int _tickIndex;
        private int _patternDelay;
        private int? _jumpOrder;
        private int? _breakRow;
        private int? _loopTarget;

        /// <summary>Initializes a player at an order position.</summary>
        /// <param name="module">The module, with its samples decoded.</param>
        /// <param name="rate">The output rate in frames a second.</param>
        /// <param name="startOrder">The order position to start at, <c>Music.ini</c>'s <c>Pos</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="module"/> is <see langword="null"/>.</exception>
        public SkalePlayer(SkaleModule module, int rate, int startOrder)
        {
            ArgumentNullException.ThrowIfNull(module);
            _module = module;
            _rate = rate;
            _speed = module.Speed > 0 ? module.Speed : 6;
            _bpm = module.Bpm > 0 ? module.Bpm : 125;
            _channels = new Channel[module.ChannelCount];
            for (int index = 0; index < _channels.Length; index++)
            {
                _channels[index] = new Channel();
            }

            _loopRow = new int[_channels.Length];
            _loopCount = new int[_channels.Length];
            Array.Fill(_loopCount, -1);
            _order = startOrder;
        }

        /// <summary>Renders interleaved stereo frames.</summary>
        /// <param name="interleaved">The buffer, left then right, which is filled.</param>
        public void Render(Span<float> interleaved)
        {
            int written = 0;
            while (written < interleaved.Length)
            {
                if (_tickOffset >= _tickFrames)
                {
                    NextTick();
                }

                int frames = Math.Min(_tickFrames - _tickOffset, (interleaved.Length - written) / 2);
                if (frames <= 0)
                {
                    break;
                }

                _tick.AsSpan(_tickOffset * 2, frames * 2).CopyTo(interleaved[written..]);
                _tickOffset += frames;
                written += frames * 2;
            }
        }

        private static double PeriodOf(int note, int finetune)
        {
            return MaxPeriod - (64.0 * note) - (finetune / 2);
        }

        private static double FrequencyOf(double period)
        {
            return PitchReferenceHz * Math.Pow(2, (4608.0 - period) * (1.0 / 768.0));
        }

        /// <summary>Decodes a BCD row number, preserving values with invalid BCD digits.</summary>
        private static int Bcd(int value)
        {
            int high = value >> 4;
            int low = value & 0x0F;
            return high < 10 && low < 10 ? low + (10 * high) : value;
        }

        private static (int Start, int End) LoopBounds(SkaleSample sample, int length)
        {
            int start = Math.Max(0, Math.Min(sample.LoopStart, length - 1));
            int end = Math.Max(start, Math.Min(sample.LoopEnd, length));
            return (start, end);
        }

        /// <summary>Plays one tick: the sequence steps, then the channels are mixed into the tick buffer.</summary>
        private void NextTick()
        {
            if (_pattern is null)
            {
                EnterPattern();
            }

            SkalePattern pattern = _pattern!;
            if (_tickIndex == 0)
            {
                DoRow(pattern);
            }
            else
            {
                DoTick();
            }

            AdvanceEnvelopes();
            _tickFrames = (int)(_rate / (_bpm * 0.4));
            if (_tick.Length < _tickFrames * 2)
            {
                _tick = new float[_tickFrames * 2];
            }

            Array.Clear(_tick, 0, _tickFrames * 2);
            Mix(_tickFrames);
            _tickOffset = 0;
            _tickIndex++;

            // Re-read the speed every tick so an Fxx effect shortens or lengthens its row.
            if (_tickIndex < _speed)
            {
                return;
            }

            _tickIndex = 0;
            if (_patternDelay > 0)
            {
                _patternDelay--;
                return;
            }

            if (_loopTarget is int target)
            {
                _row = target;
                _loopTarget = null;
                return;
            }

            if (_jumpOrder is int jump)
            {
                _jumpOrder = null;
                _breakRow = null;
                _order = jump;
                _pattern = null;
                return;
            }

            if (_breakRow is not null)
            {
                _breakRow = null;
                _order++;
                _pattern = null;
                return;
            }

            _row++;
            if (_row >= pattern.Rows)
            {
                _order++;
                _pattern = null;
            }
        }

        /// <summary>Moves to the order's pattern, skipping missing ones and wrapping past the song's end.</summary>
        private void EnterPattern()
        {
            for (int attempt = 0; attempt <= _module.SongLength + 1; attempt++)
            {
                if (_order < 0 || _order >= Math.Max(_module.SongLength, 1))
                {
                    _order = 0;
                }

                if (_module.Patterns.TryGetValue(_module.Order[_order], out SkalePattern? pattern) && pattern.Rows > 0)
                {
                    _pattern = pattern;
                    _row = 0;
                    _tickIndex = 0;
                    return;
                }

                _order++;
            }

            throw new InvalidOperationException("The module has no playable pattern.");
        }

        private SkaleInstrument? InstrumentAt(int index)
        {
            return _module.Instruments.GetValueOrDefault(index);
        }

        /// <summary>Triggers or releases a note and returns whether the channel was addressed.</summary>
        private bool Trigger(int index, int note, int cellInstrument, int volume, bool restartEnvelopes)
        {
            Channel channel = _channels[index];
            if (note == NoNote)
            {
                if (cellInstrument != 0
                    && InstrumentAt(channel.LastInstrument) is SkaleInstrument held
                    && channel.Note < 0x60
                    && held.Select(channel.Note, 127) is SkaleSample sample)
                {
                    if (restartEnvelopes)
                    {
                        StartEnvelopes(channel, held);
                    }

                    channel.Fade = FadeFull;
                    channel.KeyOff = false;
                    channel.Volume = sample.Volume;
                    channel.On = true;
                }

                return false;
            }

            if (note == NoteOff)
            {
                channel.KeyOff = true;
                if ((channel.Instrument is null || channel.Instrument.Fadeout == 0) && !channel.VolumeEnvelope.Active)
                {
                    channel.On = false;
                }

                return false;
            }

            SkaleInstrument? instrument;
            int noteVolume;
            if (cellInstrument != 0)
            {
                instrument = InstrumentAt(cellInstrument - 1);
                noteVolume = volume;
            }
            else
            {
                instrument = InstrumentAt(channel.LastInstrument);
                noteVolume = channel.Volume;
            }

            if (instrument is null)
            {
                return false;
            }

            StartNote(channel, instrument, note, noteVolume, restartEnvelopes);
            return true;
        }

        /// <summary>Selects and starts a note's sample, optionally restarting its envelopes.</summary>
        private static void StartNote(Channel channel, SkaleInstrument instrument, int note, int volume, bool restartEnvelopes)
        {
            int velocity = volume <= 0x7F ? volume : 127;
            SkaleSample? sample = instrument.Select(note + instrument.Transpose, velocity);
            if (sample is null || !sample.IsUsable)
            {
                return;
            }

            channel.Volume = volume >= 0x81 ? sample.Volume : volume;
            int final = instrument.FixedNote[note & 0x7F]
                ? sample.RelativeNote + instrument.Transpose + 60
                : note + instrument.Transpose + sample.RelativeNote;
            final = Math.Clamp(final, 0, 126);
            channel.Instrument = instrument;
            channel.Sample = sample;
            channel.LastInstrument = instrument.Index;
            channel.Relative = final - note;
            channel.Finetune = sample.Finetune + instrument.Finetune;
            channel.Note = final;
            channel.SetPeriod(PeriodOf(final, channel.Finetune));
            channel.Position = 0;
            channel.SampleReversed = false;
            channel.Fade = FadeFull;
            channel.KeyOff = false;
            channel.On = true;
            channel.Pan = channel.PanBase = sample.Panning;
            channel.GainLeft = 2.0 * (1.0 - instrument.Panning) * instrument.Volume;
            channel.GainRight = 2.0 * instrument.Panning * instrument.Volume;
            channel.VibratoPosition = 0;
            channel.VibratoReversed = false;
            channel.TremoloPosition = 0;
            channel.TremoloReversed = false;
            if (restartEnvelopes)
            {
                StartEnvelopes(channel, instrument);
            }
        }

        /// <summary>Starts the volume and panning envelopes with their default fallback values.</summary>
        private static void StartEnvelopes(Channel channel, SkaleInstrument instrument)
        {
            channel.VolumeEnvelope.Start(instrument.Envelope(1), 64);
            channel.PanEnvelope.Start(instrument.Envelope(2), 32);
            if (!channel.PanEnvelope.Active)
            {
                channel.Pan = channel.PanBase;
            }
        }

        /// <summary>Applies notes and effects at tick zero of a row.</summary>
        private void DoRow(SkalePattern pattern)
        {
            for (int index = 0; index < _channels.Length; index++)
            {
                Channel channel = _channels[index];
                SkaleCell? cell = pattern.Channels.TryGetValue(index, out SkaleCell[]? cells) && _row < cells.Length ? cells[_row] : null;

                // An arpeggio row ends by restoring the base note.
                if (channel.Command == 0x0B)
                {
                    channel.SetNote(channel.ArpeggioNote, PeriodOf(channel.ArpeggioNote, channel.Finetune));
                }

                channel.PreviousCommand = channel.Command;
                if (cell is not SkaleCell current)
                {
                    channel.Command = 0;
                    continue;
                }

                int note = current.Note;
                int cellInstrument = current.Instrument;
                int volumeColumn = current.Volume;
                int effect = current.Effect;
                int parameter = current.Param;

                // The per-instrument effect remap, inst+3728.
                int instrumentNumber = cellInstrument != 0
                    ? cellInstrument
                    : channel.LastInstrument != 255 ? channel.LastInstrument + 1 : 255;
                if (instrumentNumber is not 0 and not 255
                    && InstrumentAt(instrumentNumber - 1) is SkaleInstrument remapping
                    && effect < remapping.EffectMap.Length)
                {
                    effect = remapping.EffectMap[effect];
                }

                int high = parameter >> 4;
                int low = parameter & 0x0F;

                // A row that ends a vibrato puts the period back where it started.
                if (channel.PreviousCommand is 0x04 or 0x06 && effect is not 0x04 and not 0x06)
                {
                    channel.SetPeriod(channel.VibratoBase);
                }

                bool restartEnvelopes = true;
                if (effect == 0x03)
                {
                    note = NoNote;
                    restartEnvelopes = false;
                }

                int triggerVolume = effect == 0x0C ? parameter : volumeColumn & 0xFF;
                if (!(effect == 0x0E && high == 0x0D))
                {
                    _ = Trigger(index, note, cellInstrument, triggerVolume, restartEnvelopes);
                }

                if (volumeColumn < 0x81)
                {
                    channel.Volume = volumeColumn;
                }

                channel.Command = 0;
                channel.CommandParameter = 0;
                DoEffect(index, current, effect, parameter, high, low, note, cellInstrument, volumeColumn);
            }
        }

        private void DoEffect(int index, SkaleCell cell, int effect, int parameter, int high, int low, int note, int cellInstrument, int volumeColumn)
        {
            Channel channel = _channels[index];
            switch (effect)
            {
                case 0x00 when channel.On && parameter != 0:
                    channel.Command = 0x0B;
                    channel.CommandParameter = parameter;
                    channel.ArpeggioNote = channel.Note;
                    break;
                case 0x01:
                    if (parameter != 0)
                    {
                        channel.PortaUpMemory = parameter;
                    }

                    channel.Command = 0x01;
                    channel.CommandParameter = channel.PortaUpMemory;
                    break;
                case 0x02:
                    if (parameter != 0)
                    {
                        channel.PortaDownMemory = parameter;
                    }

                    channel.Command = 0x02;
                    channel.CommandParameter = channel.PortaDownMemory;
                    break;
                case 0x03:
                    if (cell.Note < NoNote)
                    {
                        channel.PortaNote = cell.Note;
                    }

                    if (channel.PortaNote != -1)
                    {
                        if (parameter != 0)
                        {
                            channel.PortaSpeed = parameter;
                        }

                        channel.Command = 0x03;
                        channel.CommandParameter = channel.PortaSpeed;
                    }

                    break;
                case 0x04:
                    int vibrato = parameter;
                    if ((parameter & 0xF0) != 0 && low != 0)
                    {
                        channel.VibratoMemory = parameter;
                    }
                    else
                    {
                        vibrato = channel.VibratoMemory;
                        if ((parameter & 0xF0) != 0)
                        {
                            vibrato = (parameter & 0xF0) | (vibrato & 0x0F);
                        }
                        else if (low != 0)
                        {
                            vibrato = low | (vibrato & 0xF0);
                        }
                    }

                    channel.VibratoMemory = vibrato;
                    if (channel.PreviousCommand is not 0x04 and not 0x06)
                    {
                        channel.VibratoBase = channel.Period;
                    }

                    channel.Command = 0x04;
                    channel.CommandParameter = vibrato;
                    break;
                case 0x05:
                    if (high != 0 || low != 0)
                    {
                        channel.VolumeSlideMemory = parameter;
                    }

                    channel.Command = 0x05;
                    channel.CommandParameter = channel.VolumeSlideMemory;
                    break;
                case 0x06:
                    if (high != 0 || low != 0)
                    {
                        channel.VolumeSlideMemory = parameter;
                    }

                    if (channel.PreviousCommand is not 0x04 and not 0x06)
                    {
                        channel.VibratoBase = channel.Period;
                    }

                    channel.Command = 0x06;
                    channel.CommandParameter = channel.VolumeSlideMemory;
                    break;
                case 0x07:
                    if (parameter != 0)
                    {
                        channel.TremoloMemory = parameter;
                    }

                    channel.Command = 0x07;
                    channel.CommandParameter = channel.TremoloMemory;
                    if (channel.PreviousCommand != 0x07)
                    {
                        channel.TremoloBase = channel.Volume;
                    }

                    break;
                case 0x08:
                    channel.Pan = channel.PanBase = parameter;
                    break;
                case 0x09:
                    int offset = parameter != 0 ? parameter << 8 : channel.OffsetMemory;
                    if (parameter != 0)
                    {
                        channel.OffsetMemory = parameter << 8;
                    }

                    SetOffset(channel, offset);
                    break;
                case 0x0A:
                    if (high != 0 || low != 0)
                    {
                        channel.VolumeSlideMemory = parameter;
                    }

                    channel.Command = 0x0A;
                    channel.CommandParameter = channel.VolumeSlideMemory;
                    break;
                case 0x0B:
                    _jumpOrder ??= parameter;
                    break;
                case 0x0C:
                    channel.Volume = Math.Min(parameter, 0x80);
                    break;
                case 0x0D:
                    _breakRow ??= Bcd(parameter);
                    break;
                case 0x0E:
                    DoExtended(index, high, low, note, cellInstrument, volumeColumn);
                    break;
                case 0x0F when parameter >= 0x20:
                    _bpm = parameter;
                    break;
                case 0x0F when parameter != 0:
                    _speed = parameter;
                    break;
                default:
                    break;
            }
        }

        private void DoExtended(int index, int high, int low, int note, int cellInstrument, int volumeColumn)
        {
            Channel channel = _channels[index];
            switch (high)
            {
                case 0x01:
                    channel.SetPeriod(channel.Period - (4.0 * low));
                    break;
                case 0x02:
                    channel.SetPeriod(channel.Period + (4.0 * low));
                    break;
                case 0x06 when low != 0:
                    if (_loopCount[index] == -1)
                    {
                        _loopCount[index] = low;
                        _loopTarget = _loopRow[index];
                    }
                    else
                    {
                        _loopCount[index]--;
                        if (_loopCount[index] <= 0)
                        {
                            _loopCount[index] = -1;
                        }
                        else
                        {
                            _loopTarget = _loopRow[index];
                        }
                    }

                    break;
                case 0x06:
                    _loopRow[index] = _row;
                    break;
                case 0x09:
                    channel.Command = 0xE9;
                    channel.CommandParameter = low;
                    channel.DelayNote = note;
                    channel.DelayInstrument = cellInstrument;
                    channel.DelayVolume = volumeColumn & 0xFF;
                    break;
                case 0x0A:
                    channel.Volume = Math.Min(128, channel.Volume + (2 * low));
                    break;
                case 0x0B:
                    channel.Volume = Math.Max(0, channel.Volume - (2 * low));
                    break;
                case 0x0C:
                    channel.Command = 0xEC;
                    channel.CommandParameter = low;
                    break;
                case 0x0D:
                    channel.Command = 0xED;
                    channel.CommandParameter = low;
                    channel.DelayNote = note;
                    channel.DelayInstrument = cellInstrument;
                    channel.DelayVolume = volumeColumn & 0xFF;
                    break;
                case 0x0E:
                    _patternDelay = low;
                    break;
                default:
                    break;
            }
        }

        /// <summary>Sets the sample playback offset while respecting its bounds and loop.</summary>
        private static void SetOffset(Channel channel, int frames)
        {
            SkaleSample? sample = channel.Sample;
            if (sample is null || !sample.IsUsable)
            {
                return;
            }

            int length = sample.Pcm!.Length;
            (int loopStart, int loopEnd) = LoopBounds(sample, length);
            if (sample.LoopType == SkaleSample.NoLoop)
            {
                if (frames < length)
                {
                    channel.Position = frames;
                }
            }
            else if (frames <= loopEnd)
            {
                channel.Position = frames;
            }
            else if (sample.LoopType == SkaleSample.ForwardLoop)
            {
                channel.Position = loopStart;
            }
        }

        /// <summary>Applies ongoing effects on ticks one through speed minus one.</summary>
        private void DoTick()
        {
            int tick = _tickIndex;
            for (int index = 0; index < _channels.Length; index++)
            {
                Channel channel = _channels[index];
                int parameter = channel.CommandParameter;
                switch (channel.Command)
                {
                    case 0x01:
                        channel.SetPeriod(channel.Period - (4.0 * parameter));
                        break;
                    case 0x02:
                        channel.SetPeriod(channel.Period + (4.0 * parameter));
                        break;
                    case 0x03:
                        TonePortamento(channel);
                        break;
                    case 0x04:
                        Vibrato(channel);
                        break;
                    case 0x05:
                        TonePortamento(channel);
                        VolumeSlide(channel);
                        break;
                    case 0x06:
                        Vibrato(channel);
                        VolumeSlide(channel);
                        break;
                    case 0x07:
                        Tremolo(channel);
                        break;
                    case 0x0A:
                        VolumeSlide(channel);
                        break;
                    case 0x0B:
                        int note = (tick % 3) switch
                        {
                            0 => channel.ArpeggioNote,
                            1 => channel.ArpeggioNote + (parameter & 0x0F),
                            _ => channel.ArpeggioNote + (parameter >> 4),
                        };
                        channel.SetNote(note, PeriodOf(note, channel.Finetune));
                        break;
                    case 0xE9 when parameter != 0 && tick % parameter == 0:
                        _ = Trigger(index, channel.DelayNote, channel.DelayInstrument, channel.DelayVolume, true);
                        break;
                    case 0xEC when tick >= parameter:
                        channel.Volume = 0;
                        break;
                    case 0xED when tick >= parameter:
                        _ = Trigger(index, channel.DelayNote, channel.DelayInstrument, channel.DelayVolume, true);
                        channel.Command = 0;
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>Slides the current period toward the portamento target without overshooting.</summary>
        private static void TonePortamento(Channel channel)
        {
            if (channel.PortaNote < 0)
            {
                return;
            }

            double target = MaxPeriod - (64.0 * Math.Max(0, channel.PortaNote + channel.Relative)) - (channel.Finetune / 2);
            double step = 4.0 * channel.PortaSpeed;
            double period = channel.Period < target ? Math.Min(channel.Period + step, target) : Math.Max(channel.Period - step, target);

            if (period != channel.Period)
            {
                channel.SetPeriod(period);
            }
        }

        /// <summary>Modulates pitch using the vibrato depth, speed and waveform position.</summary>
        private static void Vibrato(Channel channel)
        {
            int depth = channel.CommandParameter & 0x0F;
            int speed = (channel.CommandParameter >> 4) & 0x0F;
            double delta = 4.0 * (depth * VibratoTable[channel.VibratoPosition] / 128);
            if (channel.VibratoReversed)
            {
                delta = -delta;
            }

            channel.SetPeriod(channel.VibratoBase + delta);
            int position = channel.VibratoPosition + speed;
            if (position > 0x1F)
            {
                position -= 32;
                channel.VibratoReversed = !channel.VibratoReversed;
            }

            channel.VibratoPosition = position;
        }

        /// <summary>Modulates volume using the tremolo depth, speed and waveform position.</summary>
        private static void Tremolo(Channel channel)
        {
            int depth = channel.CommandParameter & 0x0F;
            int speed = (channel.CommandParameter >> 4) & 0x0F;
            int delta = 2 * (depth * VibratoTable[channel.TremoloPosition] / 64);
            if (channel.TremoloReversed)
            {
                delta = -delta;
            }

            channel.Volume = Math.Clamp(channel.TremoloBase + delta, 0, 128);
            int position = channel.TremoloPosition + speed;
            if (position > 0x1F)
            {
                position -= 32;
                channel.TremoloReversed = !channel.TremoloReversed;
            }

            channel.TremoloPosition = position;
        }

        /// <summary>Slides volume by two units per tick, using only one nonzero parameter nibble.</summary>
        private static void VolumeSlide(Channel channel)
        {
            int up = channel.CommandParameter >> 4;
            int down = channel.CommandParameter & 0x0F;
            if (up != 0 && down == 0)
            {
                channel.Volume = Math.Min(128, channel.Volume + (2 * up));
            }
            else if (down != 0 && up == 0)
            {
                channel.Volume = Math.Max(0, channel.Volume - (2 * down));
            }
        }

        /// <summary>Advances instrument envelopes and fade once per tick.</summary>
        private void AdvanceEnvelopes()
        {
            double rate = 50.0 / (_bpm * 0.4);
            foreach (Channel channel in _channels)
            {
                if (!channel.On)
                {
                    continue;
                }

                if (channel.VolumeEnvelope.Active)
                {
                    channel.VolumeEnvelope.Step(rate, channel.KeyOff);
                    channel.EnvelopeValue = Math.Clamp(channel.VolumeEnvelope.Value, 0, 64);
                }
                else
                {
                    channel.EnvelopeValue = 64;
                }

                if (channel.PanEnvelope.Active)
                {
                    channel.PanEnvelope.Step(rate, channel.KeyOff);

                    // The envelope swings the base pan by (env - 32) * (128 - |base - 128|) / 32.
                    int swing = ((channel.PanEnvelope.Value - 32) * (128 - Math.Abs(channel.PanBase - 128))) >> 5;
                    channel.Pan = Math.Clamp(channel.PanBase + swing, 0, 255);
                }

                if (channel.KeyOff)
                {
                    int fade = channel.Instrument?.Fadeout ?? 0;
                    if (fade != 0)
                    {
                        channel.Fade -= 2 * fade;
                        if (channel.Fade <= 0)
                        {
                            channel.Fade = 0;
                            channel.On = false;
                        }
                    }
                    else if (channel.VolumeEnvelope.Active && channel.EnvelopeValue <= 0)
                    {
                        channel.On = false;
                    }
                }
            }
        }

        /// <summary>Mixes every playing channel into the tick buffer.</summary>
        private void Mix(int frames)
        {
            foreach (Channel channel in _channels)
            {
                if (!channel.On || channel.Sample is not { IsUsable: true } sample)
                {
                    continue;
                }

                float[] pcm = sample.Pcm!;
                double step = FrequencyOf(channel.Period) / _rate;
                int count = Positions(channel, sample, frames, step, out double[] positions);
                if (count == 0)
                {
                    continue;
                }

                (double targetLeft, double targetRight) = channel.TargetGains();
                double startLeft = channel.CurrentLeft;
                double startRight = channel.CurrentRight;
                channel.CurrentLeft = targetLeft;
                channel.CurrentRight = targetRight;
                for (int frame = 0; frame < count; frame++)
                {
                    double position = positions[frame];
                    int whole = (int)position;
                    whole = Math.Clamp(whole, 0, pcm.Length - 2);
                    double fraction = position - whole;
                    double value = (pcm[whole] * (1.0 - fraction)) + (pcm[whole + 1] * fraction);
                    _tick[frame * 2] += (float)(value * Ramp(startLeft, targetLeft, frame, count));
                    _tick[(frame * 2) + 1] += (float)(value * Ramp(startRight, targetRight, frame, count));
                }
            }
        }

        /// <summary>
        /// The mixing gain slews by 1/128 of the gap a frame, for at most 128
        /// frames, then holds the target.
        /// </summary>
        private static double Ramp(double current, double target, int frame, int count)
        {
            return current == target || count == 0 || frame >= Math.Min(count, 128)
                ? target
                : current + ((target - current) / 128.0 * (frame + 1));
        }

        /// <summary>A tick's sample positions, with the sample's loop applied.</summary>
        private static int Positions(Channel channel, SkaleSample sample, int frames, double step, out double[] positions)
        {
            int length = sample.Pcm!.Length;
            (int loopStart, int loopEnd) = LoopBounds(sample, length);
            positions = new double[frames];
            if (sample.LoopType == SkaleSample.ForwardLoop && loopEnd > loopStart)
            {
                double span = loopEnd - loopStart;
                for (int frame = 0; frame < frames; frame++)
                {
                    double position = channel.Position + (step * frame);
                    positions[frame] = position >= loopEnd ? loopStart + PositiveModulo(position - loopStart, span) : position;
                }

                double end = channel.Position + (step * frames);
                channel.Position = end >= loopEnd ? loopStart + CModulo(end - loopStart, span) : end;
                return frames;
            }

            if (sample.LoopType == SkaleSample.PingPongLoop && loopEnd > loopStart)
            {
                int whole = (int)Math.Floor(channel.Position);
                double fraction = channel.Position - whole;
                for (int frame = 0; frame < frames; frame++)
                {
                    if (channel.SampleReversed && whole <= loopStart)
                    {
                        whole = loopStart;
                        channel.SampleReversed = false;
                    }
                    else if (!channel.SampleReversed && whole >= loopEnd)
                    {
                        whole = loopEnd - 1;
                        channel.SampleReversed = true;
                    }

                    positions[frame] = whole + (channel.SampleReversed ? -fraction : fraction);
                    double next = fraction + step;
                    int advance = (int)next;
                    whole += channel.SampleReversed ? -advance : advance;
                    fraction = next - advance;
                }

                channel.Position = whole + fraction;
                return frames;
            }

            int count = 0;
            for (int frame = 0; frame < frames; frame++)
            {
                double position = channel.Position + (step * frame);
                if (position < length - 1)
                {
                    positions[count++] = position;
                }
            }

            if (count < frames)
            {
                channel.On = false;
            }

            channel.Position += step * frames;
            return count;
        }

        /// <summary>numpy's <c>mod</c>: the result takes the divisor's sign.</summary>
        private static double PositiveModulo(double value, double divisor)
        {
            double result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        /// <summary>Python's <c>math.fmod</c>: the result takes the dividend's sign.</summary>
        private static double CModulo(double value, double divisor)
        {
            return value % divisor;
        }

        /// <summary>One replay channel; comments in the DLL port name its offsets.</summary>
        private sealed class Channel
        {
            public SkaleInstrument? Instrument
            {
                get; set;
            }

            public SkaleSample? Sample
            {
                get; set;
            }

            public bool On
            {
                get; set;
            }

            public double Position
            {
                get; set;
            }

            public bool SampleReversed
            {
                get; set;
            }

            public double Period { get; private set; } = MaxPeriod;

            public int Note
            {
                get; set;
            }

            public int Relative
            {
                get; set;
            }

            public int Finetune
            {
                get; set;
            }

            public int Volume
            {
                get; set;
            }

            public int EnvelopeValue { get; set; } = 64;

            public int Fade { get; set; } = FadeFull;

            public bool KeyOff
            {
                get; set;
            }

            public int Pan { get; set; } = 128;

            public int PanBase { get; set; } = 128;

            public double GainLeft { get; set; } = 1;

            public double GainRight { get; set; } = 1;

            public EnvelopeState VolumeEnvelope { get; } = new();

            public EnvelopeState PanEnvelope { get; } = new();

            public int Command
            {
                get; set;
            }

            public int CommandParameter
            {
                get; set;
            }

            public int PreviousCommand
            {
                get; set;
            }

            public int PortaUpMemory
            {
                get; set;
            }

            public int PortaDownMemory
            {
                get; set;
            }

            public int PortaNote { get; set; } = -1;

            public int PortaSpeed
            {
                get; set;
            }

            public int VibratoMemory
            {
                get; set;
            }

            public int VolumeSlideMemory
            {
                get; set;
            }

            public int TremoloMemory
            {
                get; set;
            }

            public int OffsetMemory
            {
                get; set;
            }

            public int LastInstrument { get; set; } = 255;

            public int VibratoPosition
            {
                get; set;
            }

            public bool VibratoReversed
            {
                get; set;
            }

            public double VibratoBase { get; set; } = MaxPeriod;

            public int TremoloPosition
            {
                get; set;
            }

            public bool TremoloReversed
            {
                get; set;
            }

            public int TremoloBase
            {
                get; set;
            }

            public int ArpeggioNote
            {
                get; set;
            }

            public int DelayNote
            {
                get; set;
            }

            public int DelayInstrument
            {
                get; set;
            }

            public int DelayVolume
            {
                get; set;
            }

            public double CurrentLeft
            {
                get; set;
            }

            public double CurrentRight
            {
                get; set;
            }

            /// <summary>Clamps the period, then derives its note number.</summary>
            public void SetPeriod(double period)
            {
                Period = Math.Clamp(period, 0, MaxPeriod);
                Note = (int)((MaxPeriod - Period) * 0.015625);
            }

            /// <summary>Sets the note and period, applying the period limits.</summary>
            public void SetNote(int note, double period)
            {
                Note = note;
                SetPeriod(period);
            }

            /// <summary>Computes stereo gains from fade, volume, envelope, pan and instrument scaling.</summary>
            public (double Left, double Right) TargetGains()
            {
                int volume = (int)((long)Fade * Volume * EnvelopeValue / 0x400000);
                int left = Math.Min(255 - Pan, 128);
                int right = Math.Min(Pan, 128);
                return (volume * left * MixScale * GainLeft, volume * right * MixScale * GainRight);
            }
        }

        /// <summary>Evaluates an envelope at its current position, then advances it.</summary>
        private sealed class EnvelopeState
        {
            private SkaleEnvelope? _envelope;
            private double _position;

            public int Value { get; private set; } = 64;

            public bool Active
            {
                get; private set;
            }

            public void Start(SkaleEnvelope? envelope, int fallback)
            {
                if (envelope is { Enabled: true, Points.Count: > 0 })
                {
                    _envelope = envelope;
                    _position = 0;
                    Active = true;
                    Step(0, false);
                }
                else
                {
                    _envelope = null;
                    Active = false;
                    Value = fallback;
                }
            }

            public void Step(double rate, bool keyOff)
            {
                if (!Active || _envelope is not SkaleEnvelope envelope)
                {
                    return;
                }

                List<(int X, int Y)> points = envelope.Points;
                int count = points.Count;
                double advance = envelope.TempoScaled ? rate : 1.0;
                int index = 0;
                for (int point = 0; point < count; point++)
                {
                    if (points[point].X <= _position)
                    {
                        index = point;
                    }
                }

                double value = points[index].Y;
                if (index < count - 1)
                {
                    double x0 = points[index].X;
                    double dx = points[index + 1].X - x0;
                    if (dx != 0)
                    {
                        double t = (_position - x0) / dx;
                        value = ((1.0 - t) * points[index].Y) + (t * points[index + 1].Y);
                    }

                    _position += advance;
                    if (envelope.SustainOn && !keyOff)
                    {
                        int sustain = Math.Min(envelope.SustainPoint, count - 1);
                        if (points[sustain].X <= _position)
                        {
                            _position = points[sustain].X;
                        }
                    }

                    if (envelope.LoopOn)
                    {
                        int loopEnd = Math.Min(envelope.LoopEnd, count - 1);
                        int loopStart = Math.Min(envelope.LoopStart, count - 1);
                        if (points[loopEnd].X <= _position)
                        {
                            _position = points[loopStart].X;
                        }
                    }
                }

                Value = (int)value;
            }
        }
    }
}
