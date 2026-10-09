using System.Buffers.Binary;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Finds how much of a piece of the assistant's audio is speech, as opposed to the silence a line ends on.
/// </summary>
/// <remarks>
/// The model pads the end of a line with about a second of near-silence. Counted as speech, that padding pushed
/// "the goodbye has finished" a second past the last word: live, the caller sat through three and a half seconds of
/// quiet before the line was dropped, when the wait after a goodbye is meant to be two.
/// </remarks>
internal static class AssistantSpeechExtent
{
    /// <summary>
    /// How loud a window must be to count as speech. The model speaks around -38 dBFS before it is levelled, and its
    /// quietest consonants stay above this; its padding sits far below.
    /// </summary>
    internal const double SpeechLevelDbfs = -55d;

    /// <summary>
    /// The window speech is looked for in.
    /// </summary>
    internal const int WindowMilliseconds = 10;

    private static readonly double _speechRms = 32768d * Math.Pow(10, SpeechLevelDbfs / 20d);

    /// <summary>
    /// How many bytes from the start of the audio run to the end of its last speech, or zero when it holds none.
    /// </summary>
    /// <param name="pcm">16-bit little-endian PCM.</param>
    /// <param name="sampleRate">The rate of the audio.</param>
    public static int SpokenBytes(ReadOnlySpan<byte> pcm, int sampleRate = RealtimeAudioConverter.RealtimeSampleRate)
    {
        var windowBytes = Math.Max(2, sampleRate * WindowMilliseconds / 1000 * 2);
        var usable = pcm.Length - (pcm.Length % 2);

        for (var end = usable; end > 0; end -= windowBytes)
        {
            var start = Math.Max(0, end - windowBytes);

            if (Rms(pcm[start..end]) >= _speechRms)
            {
                return end;
            }
        }

        return 0;
    }

    private static double Rms(ReadOnlySpan<byte> pcm)
    {
        var samples = pcm.Length / 2;

        if (samples == 0)
        {
            return 0;
        }

        double sum = 0;

        for (var i = 0; i < samples; i++)
        {
            double sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i * 2, 2));
            sum += sample * sample;
        }

        return Math.Sqrt(sum / samples);
    }
}
