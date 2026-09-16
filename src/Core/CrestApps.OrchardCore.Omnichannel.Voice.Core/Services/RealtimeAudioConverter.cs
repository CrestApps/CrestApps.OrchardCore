using System.Buffers.Binary;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Translates between the audio a phone call carries and the audio a realtime model speaks.
/// </summary>
/// <remarks>
/// A call is 8 kHz, usually companded to μ-law because that is what the telephone network has always used; a
/// realtime model sends and expects 16-bit PCM at 24 kHz. Getting this wrong does not fail loudly — it produces
/// a caller who hears a chipmunk, a growl, or static.
/// </remarks>
public static class RealtimeAudioConverter
{
    /// <summary>
    /// The sample rate realtime sessions use, in hertz.
    /// </summary>
    public const int RealtimeSampleRate = 24_000;

    private const int MuLawBias = 0x84;
    private const int MuLawClip = 32_635;

    /// <summary>
    /// Converts a frame of caller audio into what the model expects.
    /// </summary>
    /// <param name="frame">The frame as the provider delivered it.</param>
    /// <param name="format">The format the provider is sending.</param>
    public static ReadOnlyMemory<byte> ToRealtime(ReadOnlyMemory<byte> frame, ContactCenterVoiceMediaFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (frame.IsEmpty)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var samples = Decode(frame.Span, format.Encoding);
        var resampled = Resample(samples, SampleRateOf(format), RealtimeSampleRate);

        return ToPcmBytes(resampled);
    }

    /// <summary>
    /// Converts a chunk of assistant audio into what the caller's line carries.
    /// </summary>
    /// <param name="audio">The assistant audio, 16-bit PCM at the realtime rate.</param>
    /// <param name="format">The format the provider expects to be written.</param>
    public static ReadOnlyMemory<byte> FromRealtime(ReadOnlyMemory<byte> audio, ContactCenterVoiceMediaFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (audio.IsEmpty)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var samples = FromPcmBytes(audio.Span);
        var resampled = Resample(samples, RealtimeSampleRate, SampleRateOf(format));

        return Encode(resampled, format.Encoding);
    }

    /// <summary>
    /// Resamples 16-bit samples between two rates, band-limiting to the telephone passband on the way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The band-limiting is the point, not a refinement. Going from the model's 24 kHz to the line's 8 kHz throws
    /// away two of every three samples, and anything above 4 kHz that is still in the signal when that happens
    /// does not disappear — it <em>folds back</em> into the voice band as inharmonic tones that track the speech
    /// but belong to no human throat. It is loudest on sibilants and hard consonants, and it is heard as a
    /// metallic, buzzy edge: the single most "synthetic" thing about the audio, and nothing to do with the model.
    /// </para>
    /// <para>
    /// An earlier revision skipped the filter on the reasoning that the aliasing "sits above what the line
    /// carries anyway". That is exactly backwards, and is why callers described a natural-sounding model as
    /// robotic.
    /// </para>
    /// <para>
    /// Going the other way, interpolating 8 kHz up to 24 kHz leaves mirror images of the speech above the
    /// original band; filtering them off gives the model a cleaner signal to transcribe and to run its turn
    /// detection over.
    /// </para>
    /// </remarks>
    /// <param name="samples">The samples.</param>
    /// <param name="fromRate">The rate they are at.</param>
    /// <param name="toRate">The rate they are wanted at.</param>
    public static short[] Resample(short[] samples, int fromRate, int toRate)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (fromRate == toRate || samples.Length == 0 || fromRate <= 0 || toRate <= 0)
        {
            return samples;
        }

        // Filter before throwing samples away, so what would alias is gone before it can fold.
        if (toRate < fromRate)
        {
            samples = BandLimit(samples, fromRate, toRate);
        }

        // Round rather than truncate: dropping a partial sample every frame accumulates into a drift the caller
        // eventually hears as the assistant speaking slightly too fast.
        var length = (int)Math.Round((double)samples.Length * toRate / fromRate, MidpointRounding.AwayFromZero);

        if (length <= 0)
        {
            return [];
        }

        var result = new short[length];
        var ratio = (double)(samples.Length - 1) / Math.Max(1, length - 1);

        for (var i = 0; i < length; i++)
        {
            var position = i * ratio;
            var index = (int)position;
            var next = Math.Min(index + 1, samples.Length - 1);
            var fraction = position - index;

            result[i] = (short)Math.Clamp(
                samples[index] + ((samples[next] - samples[index]) * fraction),
                short.MinValue,
                short.MaxValue);
        }

        // Interpolating upwards leaves images of the speech above its original band; clear them at the new rate.
        if (toRate > fromRate)
        {
            result = BandLimit(result, toRate, fromRate);
        }

        return result;
    }

    /// <summary>
    /// The top of the telephone passband, in hertz. A call carries roughly 300 Hz to 3.4 kHz; nothing above this
    /// survives the line, so nothing above it should be allowed to fold back into what does.
    /// </summary>
    private const double TelephonePassbandHz = 3_400d;

    /// <summary>
    /// Removes everything above the telephone passband, so a rate change cannot fold it back into the voice.
    /// </summary>
    /// <remarks>
    /// Two cascaded second-order Butterworth sections. One is too gentle to have cleared the offending band by
    /// the time it folds; four poles reach it without the cost or the dependency of a windowed-sinc design, and
    /// the whole thing is a handful of multiplies per sample on a signal already being resampled.
    /// </remarks>
    /// <param name="samples">The samples to filter.</param>
    /// <param name="sampleRate">The rate the samples are at.</param>
    /// <param name="otherRate">The rate on the other side of the conversion.</param>
    private static short[] BandLimit(short[] samples, int sampleRate, int otherRate)
    {
        // Nyquist of the narrower side, with margin, but never above the telephone passband.
        var cutoff = Math.Min(TelephonePassbandHz, Math.Min(sampleRate, otherRate) * 0.45d);

        if (cutoff <= 0 || cutoff >= sampleRate / 2d)
        {
            return samples;
        }

        var filtered = ApplyBiquad(samples, sampleRate, cutoff);

        return ApplyBiquad(filtered, sampleRate, cutoff);
    }

    /// <summary>
    /// Applies one second-order Butterworth low-pass section.
    /// </summary>
    /// <param name="samples">The samples to filter.</param>
    /// <param name="sampleRate">The rate the samples are at.</param>
    /// <param name="cutoffHz">The corner frequency.</param>
    private static short[] ApplyBiquad(short[] samples, int sampleRate, double cutoffHz)
    {
        var omega = 2d * Math.PI * cutoffHz / sampleRate;
        var cosOmega = Math.Cos(omega);

        // Q for a Butterworth (maximally flat) response.
        var alpha = Math.Sin(omega) / (2d * 0.70710678d);

        var a0 = 1d + alpha;
        var b0 = (1d - cosOmega) / 2d / a0;
        var b1 = (1d - cosOmega) / a0;
        var b2 = b0;
        var a1 = -2d * cosOmega / a0;
        var a2 = (1d - alpha) / a0;

        var result = new short[samples.Length];
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;

        for (var i = 0; i < samples.Length; i++)
        {
            double x = samples[i];
            var y = (b0 * x) + (b1 * x1) + (b2 * x2) - (a1 * y1) - (a2 * y2);

            x2 = x1;
            x1 = x;
            y2 = y1;
            y1 = y;

            result[i] = (short)Math.Clamp(y, short.MinValue, short.MaxValue);
        }

        return result;
    }

    /// <summary>
    /// Encodes one 16-bit sample as μ-law.
    /// </summary>
    /// <param name="sample">The sample.</param>
    public static byte EncodeMuLawSample(short sample)
    {
        var sign = (sample >> 8) & 0x80;

        if (sign != 0)
        {
            sample = (short)-sample;
        }

        if (sample > MuLawClip)
        {
            sample = MuLawClip;
        }

        var value = sample + MuLawBias;
        var exponent = 7;

        for (var mask = 0x4000; (value & mask) == 0 && exponent > 0; exponent--, mask >>= 1)
        {
        }

        var mantissa = (value >> (exponent + 3)) & 0x0F;

        return (byte)~(sign | (exponent << 4) | mantissa);
    }

    /// <summary>
    /// Decodes one μ-law byte into a 16-bit sample.
    /// </summary>
    /// <param name="value">The μ-law byte.</param>
    public static short DecodeMuLawSample(byte value)
    {
        var inverted = ~value;
        var sign = inverted & 0x80;
        var exponent = (inverted >> 4) & 0x07;
        var mantissa = inverted & 0x0F;

        var sample = ((mantissa << 3) + MuLawBias) << exponent;
        sample -= MuLawBias;

        return (short)(sign != 0 ? -sample : sample);
    }

    private static int SampleRateOf(ContactCenterVoiceMediaFormat format)
        => format.SampleRate > 0 ? format.SampleRate : 8_000;

    private static short[] Decode(ReadOnlySpan<byte> data, ContactCenterVoiceMediaEncoding encoding)
    {
        if (encoding is ContactCenterVoiceMediaEncoding.MuLaw or ContactCenterVoiceMediaEncoding.ALaw)
        {
            var samples = new short[data.Length];

            for (var i = 0; i < data.Length; i++)
            {
                samples[i] = encoding == ContactCenterVoiceMediaEncoding.MuLaw
                    ? DecodeMuLawSample(data[i])
                    : DecodeALawSample(data[i]);
            }

            return samples;
        }

        return FromPcmBytes(data);
    }

    private static ReadOnlyMemory<byte> Encode(short[] samples, ContactCenterVoiceMediaEncoding encoding)
    {
        if (encoding is ContactCenterVoiceMediaEncoding.MuLaw or ContactCenterVoiceMediaEncoding.ALaw)
        {
            var data = new byte[samples.Length];

            for (var i = 0; i < samples.Length; i++)
            {
                data[i] = encoding == ContactCenterVoiceMediaEncoding.MuLaw
                    ? EncodeMuLawSample(samples[i])
                    : EncodeALawSample(samples[i]);
            }

            return data;
        }

        return ToPcmBytes(samples);
    }

    private static short[] FromPcmBytes(ReadOnlySpan<byte> data)
    {
        var samples = new short[data.Length / 2];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * 2, 2));
        }

        return samples;
    }

    private static ReadOnlyMemory<byte> ToPcmBytes(short[] samples)
    {
        var data = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 2, 2), samples[i]);
        }

        return data;
    }

    private static byte EncodeALawSample(short sample)
    {
        var sign = (sample >> 8) & 0x80;

        if (sign != 0)
        {
            sample = (short)-sample;
        }

        if (sample > 32_635)
        {
            sample = 32_635;
        }

        int compressed;

        if (sample < 256)
        {
            compressed = sample >> 4;
        }
        else
        {
            var exponent = 7;

            for (var mask = 0x4000; (sample & mask) == 0 && exponent > 0; exponent--, mask >>= 1)
            {
            }

            var mantissa = (sample >> (exponent + 3)) & 0x0F;
            compressed = (exponent << 4) | mantissa;
        }

        return (byte)((compressed | sign) ^ 0x55);
    }

    private static short DecodeALawSample(byte value)
    {
        var inverted = value ^ 0x55;
        var sign = inverted & 0x80;
        var exponent = (inverted >> 4) & 0x07;
        var mantissa = inverted & 0x0F;

        var sample = exponent == 0
            ? (mantissa << 4) + 8
            : ((mantissa << 4) + 0x108) << (exponent - 1);

        return (short)(sign != 0 ? -sample : sample);
    }
}
