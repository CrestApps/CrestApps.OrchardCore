using System.Buffers.Binary;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The assistant's voice on its way to the phone line, and the caller's on its way to the model, converted as the
/// continuous streams they are rather than as unrelated chunks.
/// </summary>
/// <remarks>
/// Callers heard a faint click or scratch, most of all as the assistant finished speaking. Every chunk the model
/// delivered was converted on its own: the anti-aliasing filter started again from silence and the resampler
/// started again from the chunk's first sample, so each boundary carried a small step; the audio went out in
/// whatever length it came to rather than whole packets; and a line that ended on a non-zero sample stopped dead.
/// </remarks>
public sealed class CallAudioStreamTests
{
    private static readonly ContactCenterVoiceMediaFormat MuLaw8k = new()
    {
        Encoding = ContactCenterVoiceMediaEncoding.MuLaw,
        SampleRate = 8_000,
    };

    [Fact]
    public void TheAssistantsVoice_ConvertedInChunks_IsTheSameAsConvertedWhole()
    {
        // Arrange
        // Chunk sizes as irregular as the model's, including ones that split a 16-bit sample in half.
        var speech = Speech(milliseconds: 1_500);
        var whole = new OutgoingCallAudio(MuLaw8k);
        var chunked = new OutgoingCallAudio(MuLaw8k);

        // Act
        var expected = Concat(whole.Encode(speech), whole.Finish());
        var actual = Concat([.. Chunks(speech, 4_801, 1_203, 7, 9_600, 333).Select(chunk => chunked.Encode(chunk)), chunked.Finish()]);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TheAssistantsVoice_GoesToTheLineInWholePackets()
    {
        // Arrange
        var line = new OutgoingCallAudio(MuLaw8k);
        var writes = new List<byte[]>();

        // Act
        foreach (var chunk in Chunks(Speech(milliseconds: 1_000), 4_801, 1_203, 7, 9_600, 333))
        {
            writes.Add(line.Encode(chunk).ToArray());
        }

        writes.Add(line.Finish().ToArray());

        // Assert
        // 20 ms of 8 kHz μ-law is 160 bytes. A partial packet leaves it to the carrier to decide what fills it.
        Assert.All(writes, write => Assert.Equal(0, write.Length % 160));
        Assert.Equal(1_000 * 8, writes.Sum(write => write.Length), tolerance: 160);
    }

    [Fact]
    public void ALineThatEndsMidWord_FadesToSilence_RatherThanStoppingDead()
    {
        // Arrange
        // A loud tone cut off mid-cycle: the worst case for a click when the line goes quiet.
        var line = new OutgoingCallAudio(MuLaw8k);
        // 511 ms of 300 Hz stops 0.3 of a cycle in, close to the peak.
        var sent = Concat(line.Encode(Tone(milliseconds: 511, amplitude: 16_000)), line.Finish());

        // Act
        var samples = sent.Select(RealtimeAudioConverter.DecodeMuLawSample).ToArray();

        // Assert
        // It ends on silence, and gets there without a step: no two neighbouring samples in the last 20 ms jump
        // further than the tone itself moves between samples.
        Assert.Equal(0, Math.Abs(samples[^1]), tolerance: 8);
        var largestStep = samples[^160..].Zip(samples[^159..], (a, b) => Math.Abs(a - b)).Max();
        Assert.True(largestStep <= 6_000, $"The end of the line jumps by {largestStep}.");
    }

    [Fact]
    public void TheEndOfALine_IsPaddedWithTheCodecsSilence()
    {
        // Arrange
        // 5 ms of audio, far less than one packet, so the packet it goes in is mostly padding.
        var line = new OutgoingCallAudio(MuLaw8k);
        Assert.True(line.Encode(Silence(milliseconds: 5)).IsEmpty, "A partial packet was sent on its own.");

        // Act
        var sent = line.Finish().ToArray();

        // Assert
        // 0xFF is μ-law's zero. 0x00 is its loudest negative value.
        Assert.Equal(160, sent.Length);
        Assert.All(sent, value => Assert.Equal(RealtimeAudioConverter.EncodeMuLawSample(0), value));
        Assert.Equal(0xFF, RealtimeAudioConverter.EncodeMuLawSample(0));
    }

    [Fact]
    public void AfterTheLineIsCleared_TheNextLineFadesIn_RatherThanStartingOnAStep()
    {
        // Arrange
        var line = new OutgoingCallAudio(MuLaw8k);
        line.Encode(Tone(milliseconds: 300, amplitude: 16_000));
        line.Reset();

        // Act
        var sent = line.Encode(Constant(milliseconds: 100, value: 12_000)).ToArray();

        // Assert
        var samples = sent.Select(RealtimeAudioConverter.DecodeMuLawSample).ToArray();
        Assert.True(Math.Abs(samples[0]) < 1_000, $"The line starts at {samples[0]}.");
        Assert.True(samples.Zip(samples.Skip(1), (a, b) => Math.Abs(a - b)).Max() < 4_000);
    }

    [Fact]
    public void NothingIsSentAtTheEndOfALine_ThatLeftNothingToFinish()
    {
        // Arrange
        var line = new OutgoingCallAudio(MuLaw8k);
        line.Encode(Speech(milliseconds: 200));
        line.Reset();

        // Act & Assert
        Assert.True(line.Finish().IsEmpty);
    }

    [Fact]
    public void TheCallersVoice_ConvertedFrameByFrame_IsTheSameAsConvertedWhole()
    {
        // Arrange
        // A call delivers 20 ms at a time. Converting each on its own restarted the filter fifty times a second in
        // the audio the model listens to.
        var line = RealtimeAudioConverter.FromRealtime(Speech(milliseconds: 600), MuLaw8k).ToArray();
        var whole = new IncomingCallAudio(MuLaw8k);
        var framed = new IncomingCallAudio(MuLaw8k);

        // Act
        var expected = whole.Decode(line).ToArray();
        var actual = Concat(line.Chunk(160).Select(frame => framed.Decode(frame)).ToArray());

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(line.Length * 3 * 2, actual.Length);
    }

    private static byte[] Speech(int milliseconds)
    {
        // A voice-like mixture: two partials and a slow swell, so every sample differs from its neighbours.
        var count = RealtimeAudioConverter.RealtimeSampleRate * milliseconds / 1000;
        var samples = new short[count];

        for (var i = 0; i < count; i++)
        {
            var t = i / (double)RealtimeAudioConverter.RealtimeSampleRate;
            var swell = 0.5 + (0.5 * Math.Sin(2 * Math.PI * 3 * t));
            samples[i] = (short)(swell * ((9_000 * Math.Sin(2 * Math.PI * 220 * t)) + (3_000 * Math.Sin(2 * Math.PI * 1_310 * t))));
        }

        return Pcm(samples);
    }

    private static byte[] Tone(int milliseconds, double amplitude)
    {
        var count = RealtimeAudioConverter.RealtimeSampleRate * milliseconds / 1000;
        var samples = new short[count];

        for (var i = 0; i < count; i++)
        {
            samples[i] = (short)(amplitude * Math.Sin(2 * Math.PI * 300 * i / RealtimeAudioConverter.RealtimeSampleRate));
        }

        return Pcm(samples);
    }

    private static byte[] Constant(int milliseconds, short value)
        => Pcm(Enumerable.Repeat(value, RealtimeAudioConverter.RealtimeSampleRate * milliseconds / 1000).ToArray());

    private static byte[] Silence(int milliseconds)
        => new byte[RealtimeAudioConverter.RealtimeSampleRate * 2 * milliseconds / 1000];

    private static byte[] Pcm(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), samples[i]);
        }

        return bytes;
    }

    // Splits the buffer into chunks of the given sizes, cycling through them.
    private static IEnumerable<ReadOnlyMemory<byte>> Chunks(byte[] buffer, params int[] sizes)
    {
        var offset = 0;

        for (var i = 0; offset < buffer.Length; i++)
        {
            var size = Math.Min(sizes[i % sizes.Length], buffer.Length - offset);

            yield return buffer.AsMemory(offset, size);

            offset += size;
        }
    }

    private static byte[] Concat(params ReadOnlyMemory<byte>[] parts)
        => parts.SelectMany(part => part.ToArray()).ToArray();
}
