using System.Buffers.Binary;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The guard that keeps the assistant's own voice, leaking back up the caller's line, from reaching the model as
/// if the caller had said something.
/// </summary>
/// <remarks>
/// Both halves matter equally. A live call opened with the model answering "Bye-bye." and "you" — words nobody
/// said, transcribed from its own greeting coming back off the line — and sounded like it was talking to itself.
/// But the fix that was tried before that, making the detector harder to trigger, clipped a real "yeah" into a
/// fragment the assistant then misread and hung up on. So every test that proves echo is held back sits next to
/// one proving a real short answer, or a caller talking over the assistant, still gets through intact.
/// </remarks>
public sealed class CallerEchoGuardTests
{
    private const int FrameMilliseconds = 20;

    private static readonly long _start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    // Levels on a telephone line, in dBFS: ordinary speech into a handset, a quiet talker's first syllable, and
    // the kind of faint, garbled echo the speech recognizer hallucinates words out of.
    private const double Speech = -20d;
    private const double QuietSpeech = -44d;
    private const double Echo = -52d;

    [Fact]
    public void WithTheAssistantSilent_EvenTheQuietestAudio_ReachesTheModelUntouched()
    {
        // Arrange
        // Nothing has been played to the caller, so there is nothing that could be echo: a caller who picks up and
        // says a soft "hello?" must be heard exactly as they said it.
        var guard = new CallerEchoGuard();
        var frames = Frames(QuietSpeech, 10);

        // Act
        var released = Feed(guard, frames, startTicks: _start, assistantPlaysUntilTicks: 0);

        // Assert
        Assert.Equal(Concat(frames), Concat(released));
    }

    [Fact]
    public void AQuietShortAnswer_OnceTheAssistantHasFinished_ReachesTheModelUntouched()
    {
        // Arrange
        // The case the earlier threshold change broke: a soft "yeah" after the assistant's question. By the time
        // the echo tail is over, nothing about the caller's audio is altered, however quiet it is.
        var guard = new CallerEchoGuard();
        var assistantEnded = _start;
        var frames = Frames(QuietSpeech, 15);

        // Act
        var released = Feed(
            guard,
            frames,
            startTicks: assistantEnded + Ms(CallerEchoGuard.EchoTailMilliseconds + FrameMilliseconds),
            assistantPlaysUntilTicks: assistantEnded);

        // Assert
        Assert.Equal(Concat(frames), Concat(released));
        Assert.Equal(0, guard.WithheldMilliseconds);
    }

    [Theory]
    [InlineData(-32.3d)]
    [InlineData(-27.3d)]
    [InlineData(-30.8d)]
    public void TheQuietestHelloHeardLive_OverTheAssistant_ReachesTheModelAsSaid(double dbfs)
    {
        // Arrange
        // The levels the guard logged for a caller's "hello?" over the opening line, which was then re-read from the
        // top. The guard was not what hid them from the model: every one of them is let through intact, and the
        // several seconds it sent as silence on that call peaked at -70 dBFS, which is an empty line.
        var guard = new CallerEchoGuard();
        var frames = Frames(dbfs, 25);

        // Act
        var released = Feed(guard, frames, startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        Assert.Equal(Concat(frames), Concat(released));
        Assert.Equal(0, guard.WithheldMilliseconds);
        Assert.Equal(1, guard.Openings);
    }

    [Fact]
    public void EchoOfTheAssistant_WhileItIsSpeaking_ReachesTheModelOnlyAsSilence()
    {
        // Arrange
        // What produced "Bye-bye." on a live call: two seconds of faint echo while the greeting played.
        var guard = new CallerEchoGuard();
        var frames = Frames(Echo, 100);

        // Act
        var released = Feed(guard, frames, startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        var sent = Concat(released);

        Assert.NotEmpty(sent);
        Assert.All(sent, sample => Assert.Equal(0, sample));
        Assert.Equal(0, guard.Openings);
    }

    [Fact]
    public void EchoThatWasHeldBack_IsStillCountedAgainstTheLinesTimeline()
    {
        // Arrange
        // Held audio is replaced, not dropped: the model's view of the call has to stay in step with the clock,
        // or its own turn timing drifts. Only the short onset buffer is ever outstanding.
        var guard = new CallerEchoGuard();
        var frames = Frames(Echo, 100);

        // Act
        var released = Feed(guard, frames, startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        var outstanding = Concat(frames).Length - Concat(released).Length;

        Assert.InRange(outstanding, 0, BytesFor(CallerEchoGuard.PreRollMilliseconds));
    }

    [Fact]
    public void EchoStillComingBack_JustAfterTheAssistantStops_IsHeldBackToo()
    {
        // Arrange
        // The end of playback is projected from here. The caller hears the last word later than that, and its echo
        // takes as long again to come back, so the guard cannot stop at the projected end.
        var guard = new CallerEchoGuard();
        var assistantEnded = _start;
        var frames = Frames(Echo, 20);

        // Act
        var released = Feed(guard, frames, startTicks: assistantEnded, assistantPlaysUntilTicks: assistantEnded);

        // Assert
        var sent = Concat(released);

        Assert.NotEmpty(sent);
        Assert.All(sent, sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void ACallerTalkingOverTheAssistant_IsLetThrough_WithTheStartOfWhatTheySaid()
    {
        // Arrange
        // Barge-in must survive. A person who talks over the assistant is heard, and heard from their first,
        // softer syllable — not from the loud part that tripped the guard, or "no, stop" arrives as "op".
        var guard = new CallerEchoGuard();
        var echo = Frames(Echo, 10);
        var onset = Frames(QuietSpeech, 3);
        var speech = Frames(Speech, 10);

        // Act
        var released = Feed(guard, [.. echo, .. onset, .. speech], startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        var sent = Concat(released);
        var spoken = Concat([.. onset, .. speech]);

        Assert.Equal(1, guard.Openings);
        Assert.InRange(guard.LastOpeningDbfs, Speech - 1, Speech + 1);
        Assert.Equal(spoken, sent[^spoken.Length..]);

        // Nothing is left behind once the caller is through: the model is exactly in step with the line.
        Assert.Equal(Concat([.. echo, .. onset, .. speech]).Length, sent.Length);
    }

    [Fact]
    public void ASingleLoudClick_WhileTheAssistantSpeaks_DoesNotLetTheEchoThrough()
    {
        // Arrange
        // A line click is one loud frame, and a person's voice is not. Opening on it would pass the echo after it.
        var guard = new CallerEchoGuard();
        var frames = Frames(Echo, 10).Concat(Frames(-10d, 1)).Concat(Frames(Echo, 20)).ToArray();

        // Act
        var released = Feed(guard, frames, startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        Assert.Equal(0, guard.Openings);
        Assert.All(Concat(released), sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void APauseBetweenWords_DoesNotCutTheCallerOff()
    {
        // Arrange
        // People breathe mid-sentence. The quiet between two words is part of what they said.
        var guard = new CallerEchoGuard();
        var first = Frames(Speech, 5);
        var pause = Frames(Echo, 10);
        var second = Frames(Speech, 5);

        // Act
        var released = Feed(guard, [.. first, .. pause, .. second], startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        Assert.Equal(Concat([.. first, .. pause, .. second]), Concat(released));
        Assert.Equal(1, guard.Openings);
    }

    [Fact]
    public void OnceTheCallerHasStopped_TheEchoIsHeldBackAgain()
    {
        // Arrange
        // The assistant is still talking after the caller's interjection, so its echo must not ride in behind it.
        var guard = new CallerEchoGuard();
        var speech = Frames(Speech, 5);
        var echo = Frames(Echo, 60);

        // Act
        var released = Feed(guard, [.. speech, .. echo], startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        var last = released[^1].ToArray();

        Assert.All(Samples(last), sample => Assert.Equal(0, sample));
        Assert.True(guard.WithheldMilliseconds > 0);
    }

    [Fact]
    public void AQuietReplyThatStartsAsTheEchoTailEnds_KeepsItsFirstSyllable()
    {
        // Arrange
        // A caller who answers the instant the assistant stops, and softly: too quiet to count as talking over it,
        // but a real answer. The start of it falls inside the guard; it must still reach the model as said.
        var guard = new CallerEchoGuard();
        var assistantEnded = _start;
        var tailStart = assistantEnded + Ms(CallerEchoGuard.EchoTailMilliseconds - 100);
        var reply = Frames(QuietSpeech, 15);

        // Act
        var released = Feed(guard, reply, startTicks: tailStart, assistantPlaysUntilTicks: assistantEnded);

        // Assert
        var sent = Concat(released);
        var said = Concat(reply);

        Assert.Equal(said, sent[^said.Length..]);
        Assert.Equal(said.Length, sent.Length);
    }

    [Fact]
    public void WhatWasHeldBack_IsMeasured_SoTheLevelCanBeTunedFromLiveCalls()
    {
        // Arrange
        // The barge-in level is a judgement about real phone lines, and the only way to check it is to see how
        // loud the held-back audio actually was on calls.
        var guard = new CallerEchoGuard();

        // Act
        Feed(guard, Frames(Echo, 50), startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        Assert.True(guard.WithheldMilliseconds >= 500);
        Assert.InRange(guard.LoudestWithheldDbfs, Echo - 1, Echo + 1);
        Assert.Equal(0, guard.Openings);
    }

    [Fact]
    public void ACallerTalkingOverTheAssistant_IsReportedAsTalkingOverIt()
    {
        // Arrange
        // What stops the assistant when it is interrupted. The provider hears the caller start and stops the model,
        // but seconds of its speech are already queued on the line; only something that knows a real voice is on
        // the line, rather than the assistant's own echo, can safely take that queued speech back.
        var guard = new CallerEchoGuard();

        // Act
        Feed(guard, [.. Frames(Echo, 10), .. Frames(Speech, 5)], startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        Assert.True(guard.IsLettingCallerThrough);
    }

    [Fact]
    public void TheAssistantsOwnEcho_IsNeverReportedAsTheCallerTalkingOverIt()
    {
        // Arrange
        // Echo that trips the provider's speech detector must not cut the assistant off mid-sentence: that would be
        // the assistant interrupting itself, which is worse than the phantom turn the guard exists to stop.
        var guard = new CallerEchoGuard();

        // Act
        Feed(guard, Frames(Echo, 50), startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        Assert.False(guard.IsLettingCallerThrough);
    }

    [Fact]
    public void OnceTheCallerHasStoppedTalkingOverTheAssistant_ItIsNoLongerReported()
    {
        // Arrange
        var guard = new CallerEchoGuard();

        // Act
        // They said a word and stopped; the assistant carried on, and what follows is its echo again.
        Feed(guard, [.. Frames(Speech, 5), .. Frames(Echo, 60)], startTicks: _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Assert
        Assert.False(guard.IsLettingCallerThrough);
    }

    [Fact]
    public void ACallerSpeakingOnceTheAssistantHasFinished_IsNotTalkingOverIt()
    {
        // Arrange
        // With nothing playing there is nothing to interrupt, so a loud answer after the assistant's question is
        // just an answer.
        var guard = new CallerEchoGuard();
        var afterTheTail = _start + Ms(CallerEchoGuard.EchoTailMilliseconds + 100);

        // Act
        Feed(guard, Frames(Speech, 10), startTicks: afterTheTail, assistantPlaysUntilTicks: _start);

        // Assert
        Assert.False(guard.IsLettingCallerThrough);
    }

    private static List<ReadOnlyMemory<byte>> Feed(
        CallerEchoGuard guard,
        byte[][] frames,
        long startTicks,
        long assistantPlaysUntilTicks)
    {
        var released = new List<ReadOnlyMemory<byte>>();

        for (var i = 0; i < frames.Length; i++)
        {
            guard.Process(frames[i], startTicks + Ms(i * FrameMilliseconds), assistantPlaysUntilTicks, released);
        }

        return released;
    }

    private static byte[][] Frames(double dbfs, int count)
    {
        var frames = new byte[count][];

        for (var i = 0; i < count; i++)
        {
            frames[i] = Tone(dbfs, i);
        }

        return frames;
    }

    // One 20 ms frame of a 300 Hz tone at the given RMS level, as 16-bit PCM at the realtime rate. The frame index
    // shifts the phase so consecutive frames are distinct and an out-of-order release would be caught.
    private static byte[] Tone(double dbfs, int index)
    {
        var sampleRate = RealtimeAudioConverter.RealtimeSampleRate;
        var count = sampleRate * FrameMilliseconds / 1000;
        var amplitude = 32767d * Math.Pow(10, dbfs / 20d) * Math.Sqrt(2);
        var bytes = new byte[count * 2];

        for (var n = 0; n < count; n++)
        {
            var t = (index * count + n) / (double)sampleRate;
            var sample = (short)Math.Clamp(Math.Round(amplitude * Math.Sin(2 * Math.PI * 300 * t)), short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(n * 2), sample);
        }

        return bytes;
    }

    private static byte[] Concat(IEnumerable<byte[]> frames)
        => frames.SelectMany(frame => frame).ToArray();

    private static byte[] Concat(IEnumerable<ReadOnlyMemory<byte>> frames)
        => frames.SelectMany(frame => frame.ToArray()).ToArray();

    private static short[] Samples(byte[] pcm)
    {
        var samples = new short[pcm.Length / 2];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2));
        }

        return samples;
    }

    private static int BytesFor(int milliseconds)
        => RealtimeAudioConverter.RealtimeSampleRate * 2 * milliseconds / 1000;

    private static long Ms(int milliseconds)
        => TimeSpan.TicksPerMillisecond * milliseconds;
}
