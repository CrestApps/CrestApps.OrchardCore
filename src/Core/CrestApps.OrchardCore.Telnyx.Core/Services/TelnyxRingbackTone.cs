namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// One cycle of the North American ringing tone (440 Hz and 480 Hz together, two seconds on and four off) as a WAV
/// file, for a caller the platform has already answered and who is now waiting for a person.
/// </summary>
/// <remarks>
/// A caller answered to hear a phone menu no longer gets the network's own ringback: to them the line is connected,
/// and silence while an agent's phone rings sounds exactly like a dropped call. The tone is generated rather than
/// hosted, so it needs no public URL and no upload, and is sent as Telnyx <c>playback_content</c> (base64 WAV, see
/// https://developers.telnyx.com/api-reference/call-commands/play-audio-url) looped until something else is played or
/// playback is stopped. It is 8 kHz, 8-bit mono PCM: the telephone band, and small enough to send on every call.
/// </remarks>
public static class TelnyxRingbackTone
{
    private const int SampleRate = 8000;
    private const int ToneSeconds = 2;
    private const int SilenceSeconds = 4;

    private static readonly Lazy<string> _base64 = new(() => Convert.ToBase64String(CreateWav()));

    /// <summary>
    /// Gets the tone as a base64 WAV, ready for <c>playback_content</c>.
    /// </summary>
    public static string Base64Wav => _base64.Value;

    /// <summary>
    /// Builds the WAV file.
    /// </summary>
    public static byte[] CreateWav()
    {
        var sampleCount = SampleRate * (ToneSeconds + SilenceSeconds);
        var toneSamples = SampleRate * ToneSeconds;
        var wav = new byte[44 + sampleCount];

        using (var stream = new MemoryStream(wav))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + sampleCount);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(SampleRate);
            writer.Write(SampleRate); // byte rate: one byte per sample
            writer.Write((short)1); // block align
            writer.Write((short)8); // bits per sample
            writer.Write("data"u8);
            writer.Write(sampleCount);

            for (var i = 0; i < sampleCount; i++)
            {
                var value = 0d;

                if (i < toneSamples)
                {
                    var t = (double)i / SampleRate;
                    value = 0.25 * (Math.Sin(2 * Math.PI * 440 * t) + Math.Sin(2 * Math.PI * 480 * t));
                }

                // 8-bit PCM is unsigned, centred on 128.
                writer.Write((byte)Math.Clamp((int)Math.Round(128 + (value * 127)), 0, 255));
            }
        }

        return wav;
    }
}
