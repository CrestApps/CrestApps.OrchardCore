using System.Buffers.Binary;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The quiet room behind an automated call: audible enough to read as a person sitting somewhere, quiet enough to
/// stay under their voice, and never the same twice.
/// </summary>
public sealed class CallAmbienceTests
{
    private const int SampleRate = 24_000;

    [Fact]
    public void TheRoomIsNeverPerfectlySilent()
    {
        // A dead-silent line between sentences is the strongest tell that nobody is there.
        var ambience = new CallAmbience(SampleRate, includeTyping: false, seed: 1);
        var samples = new short[SampleRate];

        ambience.Next(samples);

        Assert.Contains(samples, sample => sample != 0);
    }

    [Fact]
    public void TheRoomStaysQuietEnoughToTalkOver()
    {
        // The bed exists to be felt, not heard. Anything approaching speech level makes the agent harder to
        // understand, which is worse than the silence it replaces.
        var ambience = new CallAmbience(SampleRate, includeTyping: true, seed: 2);
        var samples = new short[SampleRate * 10];

        ambience.Next(samples);

        var peak = samples.Max(sample => Math.Abs((int)sample));

        // Roughly -25 dBFS. Comfortably below speech, comfortably above the noise floor of the line.
        Assert.InRange(peak, 1, (int)(short.MaxValue * 0.06));
    }

    [Fact]
    public void SomeoneIsTypingWhileTheyTalkToYou()
    {
        // Over ten seconds a burst or two should have happened, and each keystroke is a transient well above the
        // room tone it sits on.
        var withTyping = new short[SampleRate * 10];
        new CallAmbience(SampleRate, includeTyping: true, seed: 3).Next(withTyping);

        var withoutTyping = new short[SampleRate * 10];
        new CallAmbience(SampleRate, includeTyping: false, seed: 3).Next(withoutTyping);

        Assert.True(
            withTyping.Max(s => Math.Abs((int)s)) > withoutTyping.Max(s => Math.Abs((int)s)) * 2,
            "Typing should be clearly louder than the room tone it sits over.");
    }

    [Fact]
    public void TypingCanBeTurnedOffWithoutTakingTheRoomWithIt()
    {
        // Some campaigns want presence without implying the agent is doing something else while you talk.
        var ambience = new CallAmbience(SampleRate, includeTyping: false, seed: 4);
        var samples = new short[SampleRate * 10];

        ambience.Next(samples);

        var peak = samples.Max(sample => Math.Abs((int)sample));

        Assert.True(peak > 0, "The room tone should still be present.");
        Assert.True(peak < short.MaxValue * 0.02, "Without typing the bed should be only room tone.");
    }

    [Fact]
    public void TheRoomNeverRepeatsItself()
    {
        // A looped recording is its own tell: on a long call the caller starts to hear the seam. Two consecutive
        // stretches of the same length must not be identical.
        var ambience = new CallAmbience(SampleRate, includeTyping: true, seed: 5);

        var first = new short[SampleRate];
        var second = new short[SampleRate];

        ambience.Next(first);
        ambience.Next(second);

        Assert.False(first.SequenceEqual(second), "The bed must be generated, not looped.");
    }

    [Fact]
    public void MixingPutsTheRoomBehindTheVoiceWithoutQuietingIt()
    {
        // Summed, not averaged. Averaging would drop the agent's voice by half whenever the bed is present, which
        // is audible as their level dipping every time they type.
        Span<short> speech = [10_000, -10_000, 0];
        ReadOnlySpan<short> bed = [100, -100, 50];

        CallAmbience.MixInto(speech, bed);

        Assert.Equal(10_100, speech[0]);
        Assert.Equal(-10_100, speech[1]);
        Assert.Equal(50, speech[2]);
    }

    [Fact]
    public void MixingALoudPassageClipsRatherThanWrappingAround()
    {
        // Wrapping a 16-bit sample turns a loud moment into a violent crack in the caller's ear.
        Span<short> speech = [short.MaxValue, short.MinValue];
        ReadOnlySpan<short> bed = [500, -500];

        CallAmbience.MixInto(speech, bed);

        Assert.Equal(short.MaxValue, speech[0]);
        Assert.Equal(short.MinValue, speech[1]);
    }

    [Fact]
    public void MixingIntoPcmBytesLeavesTheVoiceRecognizable()
    {
        // The realtime side hands us bytes, not samples, so the byte-level path is the one the call actually uses.
        var pcm = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(0, 2), 8_000);
        BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(2, 2), -8_000);
        BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(4, 2), 0);

        new CallAmbience(SampleRate, includeTyping: true, seed: 6).MixIntoPcmBytes(pcm);

        // Each sample moved, but only by the small amount a background bed contributes.
        Assert.InRange(BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(0, 2)), 8_000 - 2_000, 8_000 + 2_000);
        Assert.InRange(BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(2, 2)), -8_000 - 2_000, -8_000 + 2_000);
    }

    [Fact]
    public void MixingIntoAnOddNumberOfBytesLeavesTheTrailingByteAlone()
    {
        // A partial sample at the end of a chunk must not be reinterpreted, or the stream desynchronizes and the
        // rest of the call turns to static.
        var pcm = new byte[3];
        pcm[2] = 0xAB;

        new CallAmbience(SampleRate, includeTyping: true, seed: 7).MixIntoPcmBytes(pcm);

        Assert.Equal(0xAB, pcm[2]);
    }

    [Fact]
    public void ARequestForNoAudioProducesNone()
    {
        var ambience = new CallAmbience(SampleRate, includeTyping: true, seed: 8);

        Assert.True(ambience.NextPcmBytes(0).IsEmpty);
    }

    [Fact]
    public void AFrameOfBedIsTheLengthTheCallExpects()
    {
        // 20 ms at the realtime rate, as 16-bit samples: the size the pump writes every tick.
        var ambience = new CallAmbience(SampleRate, includeTyping: true, seed: 9);

        Assert.Equal(480 * 2, ambience.NextPcmBytes(480).Length);
    }

    [Fact]
    public void TheSameSeedProducesTheSameRoom()
    {
        // Not a behavior the call depends on, but the property every other test here relies on.
        var first = new short[SampleRate];
        var second = new short[SampleRate];

        new CallAmbience(SampleRate, includeTyping: true, seed: 42).Next(first);
        new CallAmbience(SampleRate, includeTyping: true, seed: 42).Next(second);

        Assert.True(first.SequenceEqual(second));
    }

    [Fact]
    public void ACallDoesNotOpenOnAKeystroke()
    {
        // Opening the instant the line connects with a keystroke reads as staged. Typing starts after a beat.
        var ambience = new CallAmbience(SampleRate, includeTyping: true, seed: 10);
        var firstHalfSecond = new short[SampleRate / 2];

        ambience.Next(firstHalfSecond);

        var peak = firstHalfSecond.Max(sample => Math.Abs((int)sample));

        Assert.True(peak < short.MaxValue * 0.02, "The call should open on room tone, not on a keystroke.");
    }

    [Fact]
    public void AnImpossibleSampleRateIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CallAmbience(0));
    }
}
