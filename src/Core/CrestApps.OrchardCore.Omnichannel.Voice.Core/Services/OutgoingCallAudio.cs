using System.Buffers.Binary;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Turns the assistant's voice into what one call's line carries, as a single continuous stream: whole 20 ms
/// packets, the codec's own silence for padding, and a few milliseconds of fade wherever the voice starts or stops.
/// </summary>
/// <remarks>
/// <para>
/// Callers heard a faint click or scratch, most of all as the assistant finished speaking. Four things in the way
/// the voice was written to the line each put a small step in the signal:
/// </para>
/// <list type="bullet">
/// <item>Every model delta was converted on its own, so the anti-aliasing filter started from silence and the
/// resampler from the chunk's first sample at every boundary (see <see cref="StreamingResampler"/>).</item>
/// <item>Audio went out in whatever length a delta came to, leaving the carrier to decide what filled the rest of a
/// packet. Here only whole packets are sent, and the end of a line is padded with the codec's zero — 0xFF for
/// μ-law, whose 0x00 is its loudest negative value.</item>
/// <item>A line that ended on a non-zero sample stopped dead. <see cref="Finish"/> ramps it to silence.</item>
/// <item>After the line was cleared for a caller who talked over the assistant, the next line began wherever its
/// first sample happened to be. After <see cref="Reset"/> the voice fades in.</item>
/// </list>
/// <para>
/// One per call, shared by everything that writes to the line — the voice and the room bed — so the stream stays
/// continuous across them. Calls are serialized, because the two write from different tasks.
/// </para>
/// </remarks>
internal sealed class OutgoingCallAudio
{
    /// <summary>
    /// How long a fade in or out lasts. Long enough to take the edge off a step, far too short to hear as a fade.
    /// </summary>
    internal const int FadeMilliseconds = 5;

    private readonly Lock _lock = new();
    private readonly ContactCenterVoiceMediaEncoding _encoding;
    private readonly int _bytesPerSample;
    private readonly int _fadeSamples;
    private readonly StreamingResampler _resampler;
    private readonly List<double> _converted = [];
    private readonly List<byte> _pending = [];

    private byte? _oddByte;
    private double _last;
    private int _fadeIn;
    private bool _speaking;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutgoingCallAudio"/> class.
    /// </summary>
    /// <param name="format">The format the line carries.</param>
    public OutgoingCallAudio(ContactCenterVoiceMediaFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        var sampleRate = format.SampleRate > 0 ? format.SampleRate : 8_000;

        _encoding = format.Encoding;
        _bytesPerSample = _encoding is ContactCenterVoiceMediaEncoding.MuLaw or ContactCenterVoiceMediaEncoding.ALaw ? 1 : 2;
        _fadeSamples = Math.Max(1, sampleRate * FadeMilliseconds / 1000);
        _resampler = new StreamingResampler(RealtimeAudioConverter.RealtimeSampleRate, sampleRate);
        FrameBytes = sampleRate / 50 * _bytesPerSample;
        _fadeIn = _fadeSamples;
    }

    /// <summary>
    /// The length of one 20 ms packet on this line, in bytes. Everything returned is a whole number of them.
    /// </summary>
    public int FrameBytes { get; }

    /// <summary>
    /// Converts the next piece of the assistant's voice, returning the whole packets it completes.
    /// </summary>
    /// <param name="pcm">16-bit little-endian PCM at the realtime rate. An odd byte is kept for the next piece.</param>
    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> pcm)
    {
        lock (_lock)
        {
            var samples = TakeSamples(pcm.Span);

            if (samples.Length == 0)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            _converted.Clear();
            _resampler.Process(samples, _converted);

            foreach (var value in _converted)
            {
                var sample = value;

                if (_fadeIn > 0)
                {
                    sample *= (_fadeSamples - _fadeIn + 1) / (double)_fadeSamples;
                    _fadeIn--;
                }

                _last = sample;
                Append(sample);
            }

            _speaking = true;

            return TakeWholeFrames();
        }
    }

    /// <summary>
    /// Ends the current line: fades the voice to silence and sends the last packet, padded with the codec's silence.
    /// </summary>
    /// <remarks>
    /// Returns nothing when there is nothing to finish — a line already cleared, or no voice since the last one.
    /// </remarks>
    public ReadOnlyMemory<byte> Finish()
    {
        lock (_lock)
        {
            if (!_speaking && _pending.Count == 0)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            // From wherever the voice stopped to silence, over a few milliseconds.
            if (Math.Abs(_last) >= 1d)
            {
                for (var k = 1; k <= _fadeSamples; k++)
                {
                    Append(_last * (_fadeSamples - k) / _fadeSamples);
                }
            }

            while (_pending.Count % FrameBytes != 0)
            {
                Append(0d);
            }

            var finished = _pending.ToArray();
            ResetLocked();

            return finished;
        }
    }

    /// <summary>
    /// Drops everything not yet sent and starts a new stream, which fades in. Used when the line is cleared.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            ResetLocked();
        }
    }

    private void ResetLocked()
    {
        _pending.Clear();
        _oddByte = null;
        _last = 0;
        _fadeIn = _fadeSamples;
        _speaking = false;
        _resampler.Reset();
    }

    private short[] TakeSamples(ReadOnlySpan<byte> pcm)
    {
        if (pcm.IsEmpty)
        {
            return [];
        }

        var total = pcm.Length + (_oddByte.HasValue ? 1 : 0);
        var samples = new short[total / 2];
        Span<byte> pair = stackalloc byte[2];
        var read = 0;

        for (var i = 0; i < samples.Length; i++)
        {
            if (i == 0 && _oddByte.HasValue)
            {
                pair[0] = _oddByte.Value;
                pair[1] = pcm[0];
                read = 1;
            }
            else
            {
                pair[0] = pcm[read];
                pair[1] = pcm[read + 1];
                read += 2;
            }

            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pair);
        }

        // A sample split across two deltas is finished by the next one rather than dropped, which would shift
        // every sample after it by a byte and turn the rest of the line to noise.
        _oddByte = total % 2 == 1 ? pcm[^1] : null;

        return samples;
    }

    private void Append(double value)
    {
        var sample = (short)Math.Clamp(Math.Round(value), short.MinValue, short.MaxValue);

        switch (_encoding)
        {
            case ContactCenterVoiceMediaEncoding.MuLaw:
                _pending.Add(RealtimeAudioConverter.EncodeMuLawSample(sample));
                break;

            case ContactCenterVoiceMediaEncoding.ALaw:
                _pending.Add(RealtimeAudioConverter.EncodeALawSample(sample));
                break;

            default:
                _pending.Add((byte)(sample & 0xFF));
                _pending.Add((byte)((sample >> 8) & 0xFF));
                break;
        }
    }

    private ReadOnlyMemory<byte> TakeWholeFrames()
    {
        var whole = _pending.Count / FrameBytes * FrameBytes;

        if (whole == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var frames = _pending.GetRange(0, whole).ToArray();
        _pending.RemoveRange(0, whole);

        return frames;
    }
}

/// <summary>
/// Turns one call's incoming audio into what the model listens to, as a single continuous stream.
/// </summary>
/// <remarks>
/// A call delivers 20 ms at a time. Converted packet by packet, the filter and the resampler started again fifty
/// times a second, and the model heard a faint click at every packet boundary under the caller's voice.
/// </remarks>
internal sealed class IncomingCallAudio
{
    private readonly ContactCenterVoiceMediaEncoding _encoding;
    private readonly StreamingResampler _resampler;
    private readonly List<double> _converted = [];
    private byte? _oddByte;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncomingCallAudio"/> class.
    /// </summary>
    /// <param name="format">The format the line carries.</param>
    public IncomingCallAudio(ContactCenterVoiceMediaFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        _encoding = format.Encoding;
        _resampler = new StreamingResampler(format.SampleRate > 0 ? format.SampleRate : 8_000, RealtimeAudioConverter.RealtimeSampleRate);
    }

    /// <summary>
    /// Converts the next packet of the caller's audio into 16-bit PCM at the realtime rate.
    /// </summary>
    /// <param name="frame">The packet as the provider delivered it.</param>
    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> frame)
    {
        var samples = DecodeSamples(frame.Span);

        if (samples.Length == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        _converted.Clear();
        _resampler.Process(samples, _converted);

        var pcm = new byte[_converted.Count * 2];

        for (var i = 0; i < _converted.Count; i++)
        {
            var sample = (short)Math.Clamp(Math.Round(_converted[i]), short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2, 2), sample);
        }

        return pcm;
    }

    private short[] DecodeSamples(ReadOnlySpan<byte> data)
    {
        if (_encoding is ContactCenterVoiceMediaEncoding.MuLaw or ContactCenterVoiceMediaEncoding.ALaw)
        {
            var samples = new short[data.Length];

            for (var i = 0; i < data.Length; i++)
            {
                samples[i] = _encoding == ContactCenterVoiceMediaEncoding.MuLaw
                    ? RealtimeAudioConverter.DecodeMuLawSample(data[i])
                    : RealtimeAudioConverter.DecodeALawSample(data[i]);
            }

            return samples;
        }

        var bytes = _oddByte.HasValue ? [_oddByte.Value, .. data] : data.ToArray();
        var pcm = new short[bytes.Length / 2];

        for (var i = 0; i < pcm.Length; i++)
        {
            pcm[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * 2, 2));
        }

        _oddByte = bytes.Length % 2 == 1 ? bytes[^1] : null;

        return pcm;
    }
}
