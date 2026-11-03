using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using NLayer;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Reads a recording's audio, finds where the customer said something personal from its transcript, covers those
/// stretches with a tone, and writes the result as a WAV file.
/// </summary>
public static partial class CallRecordingAudio
{
    /// <summary>
    /// The tone's pitch, in hertz.
    /// </summary>
    public const double ToneFrequency = 1000;

    /// <summary>
    /// How much earlier than its phrase a bleep starts, so the first word is covered too.
    /// </summary>
    public const double LeadSeconds = 0.25;

    private const float ToneAmplitude = 0.2f;

    /// <summary>
    /// Decodes MP3 or 16-bit PCM WAV audio to mono samples.
    /// </summary>
    /// <param name="source">The audio.</param>
    /// <param name="format">The recording's format, such as <c>mp3</c> or <c>wav</c>.</param>
    /// <returns>The samples, from -1 to 1, and the sample rate, or <see langword="null"/> when the format is not read.</returns>
    public static (float[] Samples, int SampleRate)? Decode(Stream source, string format)
    {
        ArgumentNullException.ThrowIfNull(source);

        return format?.Trim().TrimStart('.').ToLowerInvariant() switch
        {
            "mp3" => DecodeMp3(source),
            "wav" or "wave" => DecodeWav(source),
            _ => null,
        };
    }

    /// <summary>
    /// Finds the stretches to cover: each customer phrase with personal details in it, from just before it starts to
    /// when the next phrase starts.
    /// </summary>
    /// <param name="transcript">The recording's transcript, placed on its timeline.</param>
    /// <param name="durationSeconds">The recording's length.</param>
    /// <returns>The stretches, in seconds, in order.</returns>
    public static IReadOnlyList<(double Start, double End)> FindSpans(CallRecordingTranscript transcript, double durationSeconds)
    {
        var phrases = transcript?.Phrases?.Where(phrase => phrase.OffsetSeconds is not null).OrderBy(phrase => phrase.OffsetSeconds).ToList() ?? [];
        var spans = new List<(double Start, double End)>();

        for (var i = 0; i < phrases.Count; i++)
        {
            var phrase = phrases[i];

            if (phrase.Speaker != CallRecordingTranscriptSpeaker.Customer || !HasPersonalData(phrase.Text))
            {
                continue;
            }

            var start = Math.Max(0, phrase.OffsetSeconds.Value - LeadSeconds);
            var end = i + 1 < phrases.Count ? phrases[i + 1].OffsetSeconds.Value : durationSeconds;

            if (end > start)
            {
                spans.Add((start, end));
            }
        }

        return spans;
    }

    /// <summary>
    /// Covers stretches of audio with a tone.
    /// </summary>
    /// <param name="samples">The samples, changed in place.</param>
    /// <param name="sampleRate">The sample rate.</param>
    /// <param name="spans">The stretches, in seconds.</param>
    /// <returns>How many samples were covered.</returns>
    public static int Bleep(float[] samples, int sampleRate, IEnumerable<(double Start, double End)> spans)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var covered = 0;

        foreach (var (start, end) in spans ?? [])
        {
            var from = Math.Clamp((int)(start * sampleRate), 0, samples.Length);
            var to = Math.Clamp((int)Math.Ceiling(end * sampleRate), 0, samples.Length);

            for (var i = from; i < to; i++)
            {
                samples[i] = ToneAmplitude * (float)Math.Sin(2 * Math.PI * ToneFrequency * i / sampleRate);
            }

            covered += Math.Max(0, to - from);
        }

        return covered;
    }

    /// <summary>
    /// Writes mono samples as a 16-bit PCM WAV file.
    /// </summary>
    /// <param name="target">Where to write.</param>
    /// <param name="samples">The samples.</param>
    /// <param name="sampleRate">The sample rate.</param>
    public static void WriteWav(Stream target, float[] samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(samples);

        var dataLength = samples.Length * 2;
        var header = new byte[44];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(header, 0);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), 36 + dataLength);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(header, 8);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(28), sampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(32), 2);
        BinaryPrimitives.WriteInt16LittleEndian(header.AsSpan(34), 16);
        Encoding.ASCII.GetBytes("data").CopyTo(header, 36);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(40), dataLength);
        target.Write(header);

        var buffer = new byte[8192];
        var position = 0;

        foreach (var sample in samples)
        {
            BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(position), (short)Math.Round(Math.Clamp(sample, -1f, 1f) * short.MaxValue));
            position += 2;

            if (position == buffer.Length)
            {
                target.Write(buffer, 0, position);
                position = 0;
            }
        }

        target.Write(buffer, 0, position);
    }

    /// <summary>
    /// Gets whether a line holds personal details: a placeholder the conversation's redaction left, or a long run of
    /// digits such as a card, account or phone number it did not.
    /// </summary>
    /// <param name="text">The line.</param>
    /// <returns>Whether it does.</returns>
    public static bool HasPersonalData(string text)
        => !string.IsNullOrWhiteSpace(text) && (PlaceholderPattern().IsMatch(text) || DigitsPattern().IsMatch(text));

    private static (float[] Samples, int SampleRate)? DecodeMp3(Stream source)
    {
        using var file = new MpegFile(source);
        var channels = Math.Max(1, file.Channels);
        var mono = new List<float>();
        var buffer = new float[4096 * channels];
        int read;

        while ((read = file.ReadSamples(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i + channels <= read; i += channels)
            {
                var sum = 0f;

                for (var channel = 0; channel < channels; channel++)
                {
                    sum += buffer[i + channel];
                }

                mono.Add(sum / channels);
            }
        }

        return (mono.ToArray(), file.SampleRate);
    }

    private static (float[] Samples, int SampleRate)? DecodeWav(Stream source)
    {
        using var memory = new MemoryStream();
        source.CopyTo(memory);
        var bytes = memory.GetBuffer().AsSpan(0, (int)memory.Length);

        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("RIFF"u8) || !bytes.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return null;
        }

        int channels = 0, sampleRate = 0, bits = 0, format = 0;
        var offset = 12;

        while (offset + 8 <= bytes.Length)
        {
            var id = bytes.Slice(offset, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4));
            var body = offset + 8;

            if (id.SequenceEqual("fmt "u8) && size >= 16)
            {
                format = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(body, 2));
                channels = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(body + 2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(body + 4, 4));
                bits = BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(body + 14, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (format != 1 || bits != 16 || channels < 1)
                {
                    return null;
                }

                var length = Math.Min(size, bytes.Length - body);
                var frames = length / (2 * channels);
                var samples = new float[frames];

                for (var frame = 0; frame < frames; frame++)
                {
                    var sum = 0f;

                    for (var channel = 0; channel < channels; channel++)
                    {
                        sum += BinaryPrimitives.ReadInt16LittleEndian(bytes.Slice(body + ((frame * channels) + channel) * 2, 2)) / 32768f;
                    }

                    samples[frame] = sum / channels;
                }

                return (samples, sampleRate);
            }

            offset = body + size + (size % 2);
        }

        return null;
    }

    [GeneratedRegex(@"\[(EMAIL|PHONE|CARD|ID NUMBER|NAME|ADDRESS|DATE OF BIRTH)\]", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\d(?:[\s-]?\d){6,}", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsPattern();
}
