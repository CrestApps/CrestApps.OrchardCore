using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The assistant's voice is brought to a steady level on the line. A newer realtime model spoke about 14 dB quieter
/// than the one before it, and callers asked the assistant why it sounded so low.
/// </summary>
public sealed class AssistantVoiceLevelerTests
{
    private const int SampleRate = 8_000;

    [Fact]
    public void AQuietVoice_IsRaisedToTheTargetLevel()
    {
        // Arrange: three seconds of tone at -36 dBFS, where the quieter model spoke.
        var leveler = new AssistantVoiceLeveler(SampleRate);
        var samples = Tone(seconds: 3, dbfs: -36);

        // Act
        leveler.Process(samples);

        // Assert: the last second has settled near the target.
        Assert.InRange(RmsDbfs(samples.Skip(SampleRate * 2)), AssistantVoiceLeveler.TargetDbfs - 1.5, AssistantVoiceLeveler.TargetDbfs + 1.5);
    }

    [Fact]
    public void AVoiceAlreadyAtTheTarget_IsLeftAsItIs()
    {
        var leveler = new AssistantVoiceLeveler(SampleRate);
        var samples = Tone(seconds: 2, dbfs: AssistantVoiceLeveler.TargetDbfs);

        leveler.Process(samples);

        Assert.InRange(leveler.CurrentGainDb, -0.5, 0.5);
    }

    [Fact]
    public void AnAlmostSilentVoice_IsRaisedNoMoreThanTheCap()
    {
        var leveler = new AssistantVoiceLeveler(SampleRate);
        var samples = Tone(seconds: 3, dbfs: -48);

        leveler.Process(samples);

        Assert.InRange(leveler.CurrentGainDb, AssistantVoiceLeveler.MaxGainDb - 0.5, AssistantVoiceLeveler.MaxGainDb + 0.01);
    }

    [Fact]
    public void Silence_IsNotLifted()
    {
        var leveler = new AssistantVoiceLeveler(SampleRate);
        var samples = Enumerable.Repeat(0d, SampleRate).ToList();

        leveler.Process(samples);

        Assert.All(samples, sample => Assert.Equal(0d, sample));
    }

    [Fact]
    public void ALoudPeak_IsNeverPushedPastFullScale()
    {
        // Arrange: a quiet voice that has settled at a high gain, then a sudden loud burst.
        var leveler = new AssistantVoiceLeveler(SampleRate);
        leveler.Process(Tone(seconds: 3, dbfs: -36));
        var burst = Tone(seconds: 0.2, dbfs: -3);

        // Act
        leveler.Process(burst);

        // Assert
        Assert.All(burst, sample => Assert.InRange(Math.Abs(sample), 0, 32767));
    }

    [Theory]
    [InlineData(-24d)]
    [InlineData(-21d)]
    [InlineData(-15d)]
    public void AVoiceAtOrAboveTheTarget_PassesThroughUnchanged(double dbfs)
    {
        // The model callers already heard clearly speaks here. Whatever is done for a quieter one must not touch it:
        // not its speech, and not the gaps in it.
        var leveler = new AssistantVoiceLeveler(SampleRate);
        var speech = Tone(seconds: 2, dbfs: dbfs);
        var gap = Tone(seconds: 0.5, dbfs: dbfs - 30);
        var samples = speech.Concat(gap).Concat(Tone(seconds: 1, dbfs: dbfs)).ToList();
        var original = samples.ToList();

        leveler.Process(samples);

        Assert.Equal(original, samples);
    }

    [Fact]
    public void TheGapsBetweenWords_GetHalfTheBoost()
    {
        // Arrange: a quiet voice that has settled, then the quiet between its words: breath, word tails, the room.
        var leveler = new AssistantVoiceLeveler(SampleRate);
        leveler.Process(Tone(seconds: 2, dbfs: -36));
        var speechGainDb = leveler.CurrentGainDb;

        // Act
        leveler.Process(Tone(seconds: 1, dbfs: -60));

        // Assert
        Assert.InRange(leveler.CurrentGainDb, speechGainDb / 2 - 0.5, speechGainDb / 2 + 0.5);
    }

    [Fact]
    public void TheGain_DoesNotFollowEachWord()
    {
        // Arrange: a quiet voice that has settled, then one word said 6 dB louder.
        var leveler = new AssistantVoiceLeveler(SampleRate);
        leveler.Process(Tone(seconds: 2, dbfs: -36));
        var before = leveler.CurrentGainDb;

        // Act
        leveler.Process(Tone(seconds: 0.3, dbfs: -30));

        // Assert: the gain moved by a fraction of that, so the word stays louder, as the model said it.
        Assert.InRange(before - leveler.CurrentGainDb, 0, 1.5);
    }

    private static List<double> Tone(double seconds, double dbfs)
    {
        // A sine whose RMS is the given level: amplitude = RMS * sqrt(2).
        var amplitude = Math.Pow(10, dbfs / 20) * Math.Sqrt(2) * 32768;
        var count = (int)(SampleRate * seconds);

        return Enumerable.Range(0, count)
            .Select(i => amplitude * Math.Sin(2 * Math.PI * 300 * i / SampleRate))
            .ToList();
    }

    private static double RmsDbfs(IEnumerable<double> samples)
    {
        var list = samples.ToList();
        var rms = Math.Sqrt(list.Sum(sample => sample * sample) / list.Count) / 32768;

        return 20 * Math.Log10(rms);
    }
}
