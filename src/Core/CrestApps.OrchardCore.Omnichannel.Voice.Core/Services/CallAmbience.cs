using System.Buffers.Binary;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Generates the quiet room an agent would be sitting in — faint room tone, and the sound of someone typing while
/// they talk to you — so an automated call does not arrive on a dead-silent line.
/// </summary>
/// <remarks>
/// <para>
/// Perfect silence between sentences is one of the strongest tells that nobody is there. A real agent's line
/// always carries something: the room, the building, their keyboard as they write your details down. This
/// synthesizes that bed rather than looping a recording, because a loop has its own tell — a caller on a long
/// call starts to hear the seam, and the same keystroke pattern repeating is worse than silence.
/// </para>
/// <para>
/// Levels are deliberately low. The bed should register only as "there is an office behind this person"; a caller
/// who notices the typing as a sound in its own right is being distracted, not reassured. Everything is generated
/// from a seeded random source so a test can assert exactly what comes out.
/// </para>
/// </remarks>
public sealed class CallAmbience
{
    /// <summary>
    /// The peak amplitude of a keystroke, as a fraction of full scale. Loud enough to read as a keyboard on a
    /// narrowband line, quiet enough to sit under speech.
    /// </summary>
    private const double KeystrokePeak = 0.030;

    /// <summary>
    /// The amplitude of the constant room tone, as a fraction of full scale.
    /// </summary>
    private const double RoomTonePeak = 0.006;

    // A keystroke is a sharp transient with a short body: a click that decays in a few milliseconds, over a
    // slightly longer "thock" as the key bottoms out.
    private const double KeystrokeClickDecaySeconds = 0.004;
    private const double KeystrokeBodyDecaySeconds = 0.016;
    private const double KeystrokeLengthSeconds = 0.030;

    // Typing is bursty: a few words at speed, then a pause while the person reads, listens or thinks.
    private const int MinimumBurstKeystrokes = 3;
    private const int MaximumBurstKeystrokes = 12;
    private const double MinimumKeystrokeGapSeconds = 0.070;
    private const double MaximumKeystrokeGapSeconds = 0.180;
    private const double MinimumBurstPauseSeconds = 1.5;
    private const double MaximumBurstPauseSeconds = 6.0;

    private readonly Random _random;
    private readonly int _sampleRate;
    private readonly bool _typingEnabled;

    // The keystroke currently sounding, as an offset into its own envelope. -1 when none is sounding.
    private int _keystrokeSample = -1;
    private double _keystrokeToneStep;
    private double _keystrokeTonePhase;

    // Samples remaining before the next keystroke starts, and how many are left in the current burst.
    private int _samplesUntilNextKeystroke;
    private int _keystrokesLeftInBurst;

    // One-pole low-pass state, so the room tone is a soft hiss rather than bright white noise.
    private double _roomToneState;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallAmbience"/> class.
    /// </summary>
    /// <param name="sampleRate">The sample rate to generate at, in hertz.</param>
    /// <param name="includeTyping">Whether to include keyboard typing over the room tone.</param>
    /// <param name="seed">An optional seed, so a test can generate a known bed.</param>
    public CallAmbience(int sampleRate, bool includeTyping = true, int? seed = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);

        _sampleRate = sampleRate;
        _typingEnabled = includeTyping;
        _random = seed.HasValue ? new Random(seed.Value) : Random.Shared;

        // Start mid-pause so a call does not open on a keystroke, which reads as staged.
        _keystrokesLeftInBurst = 0;
        _samplesUntilNextKeystroke = SecondsToSamples(NextDouble(MinimumBurstPauseSeconds, MaximumBurstPauseSeconds));
    }

    /// <summary>
    /// Writes the next stretch of ambience into <paramref name="destination"/>, overwriting it.
    /// </summary>
    /// <param name="destination">The buffer to fill.</param>
    public void Next(Span<short> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            var value = NextRoomToneSample();

            if (_typingEnabled)
            {
                value += NextKeystrokeSample();
            }

            destination[i] = (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);
        }
    }

    /// <summary>
    /// Mixes <paramref name="ambience"/> underneath <paramref name="destination"/> in place, so the bed sits
    /// behind whatever is already there rather than replacing it.
    /// </summary>
    /// <remarks>
    /// Summed and clamped rather than averaged: averaging would halve the speech every time the bed is present,
    /// which is audible as the agent's voice dropping in level whenever they are typing.
    /// </remarks>
    /// <param name="destination">The speech to mix into.</param>
    /// <param name="ambience">The ambience to mix under it.</param>
    public static void MixInto(Span<short> destination, ReadOnlySpan<short> ambience)
    {
        var length = Math.Min(destination.Length, ambience.Length);

        for (var i = 0; i < length; i++)
        {
            destination[i] = (short)Math.Clamp(destination[i] + ambience[i], short.MinValue, short.MaxValue);
        }
    }

    /// <summary>
    /// Produces the next <paramref name="sampleCount"/> samples of ambience as 16-bit little-endian PCM, the
    /// format the realtime side of the call already speaks.
    /// </summary>
    /// <param name="sampleCount">The number of samples to produce.</param>
    public ReadOnlyMemory<byte> NextPcmBytes(int sampleCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sampleCount);

        var samples = new short[sampleCount];
        Next(samples);

        var data = new byte[sampleCount * 2];

        for (var i = 0; i < sampleCount; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 2, 2), samples[i]);
        }

        return data;
    }

    /// <summary>
    /// Mixes ambience underneath a buffer of 16-bit little-endian PCM in place, so the agent's voice arrives with
    /// the room already behind it rather than cutting in over silence.
    /// </summary>
    /// <param name="pcm">The PCM buffer to mix into. A trailing odd byte is left untouched.</param>
    public void MixIntoPcmBytes(Span<byte> pcm)
    {
        var sampleCount = pcm.Length / 2;

        if (sampleCount == 0)
        {
            return;
        }

        var bed = new short[sampleCount];
        Next(bed);

        for (var i = 0; i < sampleCount; i++)
        {
            var slice = pcm.Slice(i * 2, 2);
            var existing = BinaryPrimitives.ReadInt16LittleEndian(slice);

            BinaryPrimitives.WriteInt16LittleEndian(slice, (short)Math.Clamp(existing + bed[i], short.MinValue, short.MaxValue));
        }
    }

    private double NextRoomToneSample()
    {
        // A one-pole low-pass over white noise. The coefficient is fixed in time rather than in samples so the
        // tone sounds the same whether it is generated at the line rate or the model's rate.
        var noise = (_random.NextDouble() * 2.0) - 1.0;
        var coefficient = Math.Exp(-2.0 * Math.PI * 500.0 / _sampleRate);

        _roomToneState = (_roomToneState * coefficient) + (noise * (1.0 - coefficient));

        return _roomToneState * RoomTonePeak;
    }

    private double NextKeystrokeSample()
    {
        if (_keystrokeSample < 0)
        {
            if (--_samplesUntilNextKeystroke > 0)
            {
                return 0.0;
            }

            StartKeystroke();
        }

        var elapsed = _keystrokeSample / (double)_sampleRate;

        if (elapsed >= KeystrokeLengthSeconds)
        {
            EndKeystroke();

            return 0.0;
        }

        _keystrokeSample++;

        // The click is broadband and dies almost immediately; the body is a short damped tone underneath it,
        // which is what makes it read as a key rather than as a pop of static.
        var click = ((_random.NextDouble() * 2.0) - 1.0) * Math.Exp(-elapsed / KeystrokeClickDecaySeconds);
        var body = Math.Sin(_keystrokeTonePhase) * Math.Exp(-elapsed / KeystrokeBodyDecaySeconds) * 0.5;

        _keystrokeTonePhase += _keystrokeToneStep;

        return (click + body) * KeystrokePeak;
    }

    private void StartKeystroke()
    {
        _keystrokeSample = 0;
        _keystrokeTonePhase = 0.0;

        // Keys differ slightly in pitch, and the same key struck twice is never identical. Without this variation
        // the typing reads as a sound effect being retriggered.
        var toneHz = NextDouble(1_800.0, 3_200.0);
        _keystrokeToneStep = 2.0 * Math.PI * toneHz / _sampleRate;

        if (_keystrokesLeftInBurst <= 0)
        {
            _keystrokesLeftInBurst = _random.Next(MinimumBurstKeystrokes, MaximumBurstKeystrokes + 1);
        }
    }

    private void EndKeystroke()
    {
        _keystrokeSample = -1;
        _keystrokesLeftInBurst--;

        // Within a burst the gaps are short; at the end of one the person stops to read or listen.
        _samplesUntilNextKeystroke = _keystrokesLeftInBurst > 0
            ? SecondsToSamples(NextDouble(MinimumKeystrokeGapSeconds, MaximumKeystrokeGapSeconds))
            : SecondsToSamples(NextDouble(MinimumBurstPauseSeconds, MaximumBurstPauseSeconds));
    }

    private int SecondsToSamples(double seconds)
        => Math.Max(1, (int)Math.Round(seconds * _sampleRate));

    private double NextDouble(double minimum, double maximum)
        => minimum + (_random.NextDouble() * (maximum - minimum));
}
