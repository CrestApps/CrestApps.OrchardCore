using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Taking the model's 24 kHz voice down to the 8 kHz line.
/// </summary>
/// <remarks>
/// A brighter realtime voice sounded harsh on the phone. Its recordings carried 6 to 8 dB more energy at the top of
/// the line's band than another model's, and the old two-section filter let a 5 kHz component -- the bright part
/// of an "s" -- fold back to 3 kHz only about 15 dB down.
/// </remarks>
public sealed class StreamingResamplerTests
{
    private const int FromRate = 24_000;
    private const int ToRate = 8_000;

    [Theory]
    [InlineData(300d)]
    [InlineData(1_000d)]
    [InlineData(2_000d)]
    public void TheHeartOfTheTelephoneBand_PassesAtItsOwnLevel(double frequency)
    {
        var output = Resample(frequency);

        Assert.InRange(RmsDbfs(output) - InputDbfs, -1d, 0.5d);
    }

    [Theory]
    [InlineData(2_500d, -1.6d)]
    [InlineData(3_000d, -3.5d)]
    [InlineData(3_300d, -5.3d)]
    public void TheTopOfTheBand_KeepsTheToneTheVoiceAlwaysHad(double frequency, double fourPoleDb)
    {
        // The sharp filter was added after the old four poles, not in place of them. In place of them, every
        // model came out a few decibels brighter at the top of the band, including the one that already sounded
        // right.
        var output = Resample(frequency);

        Assert.InRange(RmsDbfs(output) - InputDbfs, fourPoleDb - 0.6d, fourPoleDb + 0.6d);
    }

    [Theory]
    [InlineData(4_700d)]
    [InlineData(5_000d)]
    [InlineData(6_500d)]
    [InlineData(9_000d)]
    public void WhatWouldFoldIntoTheLine_IsRemoved(double frequency)
    {
        var output = Resample(frequency);

        Assert.True(RmsDbfs(output) - InputDbfs < -60d, $"{frequency} Hz came through at {RmsDbfs(output) - InputDbfs:F1} dB.");
    }

    [Fact]
    public void TheResult_IsTheSame_HoweverTheStreamIsCutUp()
    {
        var input = Tone(1_000d, seconds: 0.5);
        var whole = new List<double>();
        new StreamingResampler(FromRate, ToRate).Process(input, whole);

        var pieces = new List<double>();
        var resampler = new StreamingResampler(FromRate, ToRate);

        for (var offset = 0; offset < input.Length; offset += 377)
        {
            resampler.Process(input.AsSpan(offset, Math.Min(377, input.Length - offset)), pieces);
        }

        Assert.Equal(whole.Count, pieces.Count);

        for (var i = 0; i < whole.Count; i++)
        {
            Assert.Equal(whole[i], pieces[i], 6);
        }
    }

    private const double InputDbfs = -20d;

    private static List<double> Resample(double frequency)
    {
        var output = new List<double>();
        new StreamingResampler(FromRate, ToRate).Process(Tone(frequency, seconds: 1), output);

        // The filter's start-up is not part of the steady state being measured.
        return output.Skip(ToRate / 10).ToList();
    }

    private static short[] Tone(double frequency, double seconds)
    {
        var amplitude = Math.Pow(10, InputDbfs / 20) * Math.Sqrt(2) * 32768;
        var count = (int)(FromRate * seconds);

        return Enumerable.Range(0, count)
            .Select(i => (short)Math.Round(amplitude * Math.Sin(2 * Math.PI * frequency * i / FromRate)))
            .ToArray();
    }

    private static double RmsDbfs(List<double> samples)
    {
        var rms = Math.Sqrt(samples.Sum(sample => sample * sample) / samples.Count) / 32768;

        return 20 * Math.Log10(Math.Max(rms, 1e-12));
    }
}
