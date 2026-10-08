using System.Buffers.Binary;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// How much of the assistant's audio is speech rather than the silence a line ends on.
/// </summary>
/// <remarks>
/// Live, the model padded its goodbye with about a second of silence, and the wait after the goodbye was counted from
/// the end of that padding: the caller sat through three and a half seconds of quiet before the line dropped.
/// </remarks>
public sealed class AssistantSpeechExtentTests
{
    private const int SampleRate = 24_000;

    [Fact]
    public void SpeechFollowedByPadding_EndsWhereTheSpeechEnds()
    {
        // Arrange
        var audio = Concat(Tone(-30d, 300), Tone(-75d, 700));

        // Act
        var spoken = AssistantSpeechExtent.SpokenBytes(audio);

        // Assert
        Assert.InRange(spoken, Bytes(300), Bytes(310));
    }

    [Fact]
    public void QuietSpeech_StillCountsAsSpeech()
        => Assert.Equal(Bytes(200), AssistantSpeechExtent.SpokenBytes(Tone(-48d, 200)));

    [Fact]
    public void PaddingAlone_HoldsNoSpeech()
        => Assert.Equal(0, AssistantSpeechExtent.SpokenBytes(Tone(-75d, 500)));

    [Fact]
    public void Nothing_HoldsNoSpeech()
        => Assert.Equal(0, AssistantSpeechExtent.SpokenBytes([]));

    private static int Bytes(int milliseconds)
        => SampleRate * milliseconds / 1000 * 2;

    private static byte[] Concat(byte[] first, byte[] second)
        => [.. first, .. second];

    // A 300 Hz tone at the given RMS level, as 16-bit PCM at the realtime rate.
    private static byte[] Tone(double dbfs, int milliseconds)
    {
        var count = SampleRate * milliseconds / 1000;
        var amplitude = 32767d * Math.Pow(10, dbfs / 20d) * Math.Sqrt(2);
        var bytes = new byte[count * 2];

        for (var n = 0; n < count; n++)
        {
            var sample = (short)Math.Clamp(Math.Round(amplitude * Math.Sin(2 * Math.PI * 300 * n / SampleRate)), short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(n * 2), sample);
        }

        return bytes;
    }
}
