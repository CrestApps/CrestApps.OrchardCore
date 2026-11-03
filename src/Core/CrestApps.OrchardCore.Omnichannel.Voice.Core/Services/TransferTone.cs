namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The ringing a caller hears while their call is put through to someone else: the two tones of a ringback, two
/// seconds on and four off, made here rather than played from a recording.
/// </summary>
internal sealed class TransferTone
{
    private const double LowFrequency = 440;
    private const double HighFrequency = 480;

    // Each tone at a quarter of full scale: plainly heard, well clear of clipping when the two add up.
    private const double Amplitude = 0.25 * short.MaxValue;

    private static readonly TimeSpan _ringing = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _cycle = TimeSpan.FromSeconds(6);

    private readonly int _sampleRate;
    private long _sample;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferTone"/> class.
    /// </summary>
    /// <param name="sampleRate">The rate the tone is made at, in samples per second.</param>
    public TransferTone(int sampleRate)
    {
        _sampleRate = sampleRate;
    }

    /// <summary>
    /// Makes the next stretch of the tone, carrying on from the last one so the frames join without a click.
    /// </summary>
    /// <param name="samples">How many samples to make.</param>
    /// <returns>The samples, as 16-bit little-endian PCM.</returns>
    public byte[] NextPcmBytes(int samples)
    {
        var bytes = new byte[samples * 2];
        var ringingSamples = (long)(_ringing.TotalSeconds * _sampleRate);
        var cycleSamples = (long)(_cycle.TotalSeconds * _sampleRate);

        for (var index = 0; index < samples; index++, _sample++)
        {
            var value = 0d;

            if (_sample % cycleSamples < ringingSamples)
            {
                var seconds = (double)_sample / _sampleRate;
                value = Amplitude * (Math.Sin(2 * Math.PI * LowFrequency * seconds) + Math.Sin(2 * Math.PI * HighFrequency * seconds)) / 2;
            }

            var pcm = (short)Math.Clamp(Math.Round(value), short.MinValue, short.MaxValue);
            bytes[index * 2] = (byte)(pcm & 0xFF);
            bytes[(index * 2) + 1] = (byte)((pcm >> 8) & 0xFF);
        }

        return bytes;
    }
}
