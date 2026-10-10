using System.Buffers.Binary;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The listener that notices a caller's reply the realtime provider never reported hearing.
/// </summary>
/// <remarks>
/// Live, an assistant read an email address back, asked whether it was right, and the caller's clear "yes" a second
/// later was never reported as speech: both sides waited, and the caller had to say it again seven seconds on.
/// </remarks>
public sealed class CallerReplyListenerTests
{
    private const int FrameMilliseconds = 20;

    private static readonly long _start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
    private static readonly TimeSpan _wait = TimeSpan.FromMilliseconds(1200);

    private const double Speech = -27d;
    private const double QuietSpeech = -44d;
    private const double Line = -60d;

    [Fact]
    public void AShortYes_AfterTheAssistantFinished_ThatTheProviderNeverReported_IsUnheard()
    {
        // Arrange
        // The live case: about two hundred milliseconds of voice, a second after the assistant's question ended.
        var listener = new CallerReplyListener();
        var assistantEnded = _start;
        var replyEnds = Feed(listener, Frames(Speech, 10), assistantEnded + Ms(1_000), assistantEnded);

        // Act
        var unheard = listener.TryTakeUnheardReply(replyEnds + _wait.Ticks, providerHeardCallerTicks: _start - Ms(20_000), _wait);

        // Assert
        Assert.True(unheard);
    }

    [Fact]
    public void AQuietTalkersYes_IsStillAReply()
    {
        // Arrange
        var listener = new CallerReplyListener();
        var replyEnds = Feed(listener, Frames(QuietSpeech, 10), _start + Ms(500), _start);

        // Act
        var unheard = listener.TryTakeUnheardReply(replyEnds + _wait.Ticks, providerHeardCallerTicks: 0, _wait);

        // Assert
        Assert.True(unheard);
    }

    [Fact]
    public void AReplyTheProviderReported_IsNotUnheard()
    {
        // Arrange
        // The ordinary case: the provider reports the caller a few hundred milliseconds after they begin.
        var listener = new CallerReplyListener();
        var replyStarts = _start + Ms(1_000);
        var replyEnds = Feed(listener, Frames(Speech, 10), replyStarts, _start);

        // Act
        var unheard = listener.TryTakeUnheardReply(replyEnds + _wait.Ticks, providerHeardCallerTicks: replyStarts + Ms(300), _wait);

        // Assert
        Assert.False(unheard);
    }

    [Fact]
    public void AReply_IsGivenTheProviderTheWaitBeforeItCountsAsUnheard()
    {
        // Arrange
        var listener = new CallerReplyListener();
        var replyEnds = Feed(listener, Frames(Speech, 10), _start + Ms(1_000), _start);

        // Act
        var tooSoon = listener.TryTakeUnheardReply(replyEnds + _wait.Ticks - Ms(100), providerHeardCallerTicks: 0, _wait);
        var inTime = listener.TryTakeUnheardReply(replyEnds + _wait.Ticks, providerHeardCallerTicks: 0, _wait);

        // Assert
        Assert.False(tooSoon);
        Assert.True(inTime);
    }

    [Fact]
    public void AnUnheardReply_IsTakenOnlyOnce()
    {
        // Arrange
        var listener = new CallerReplyListener();
        var replyEnds = Feed(listener, Frames(Speech, 10), _start + Ms(1_000), _start);
        var now = replyEnds + _wait.Ticks;

        // Act
        var first = listener.TryTakeUnheardReply(now, providerHeardCallerTicks: 0, _wait);
        var second = listener.TryTakeUnheardReply(now + Ms(1_000), providerHeardCallerTicks: 0, _wait);

        // Assert
        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void VoiceWhileTheAssistantIsStillPlaying_IsNotAReply()
    {
        // Arrange
        // Echo, or the caller talking over the assistant: both have their own handling, and neither is an answer.
        var listener = new CallerReplyListener();
        var replyEnds = Feed(listener, Frames(Speech, 20), _start, assistantPlaysUntilTicks: _start + Ms(5_000));

        // Act
        var unheard = listener.TryTakeUnheardReply(replyEnds + _wait.Ticks, providerHeardCallerTicks: 0, _wait);

        // Assert
        Assert.False(unheard);
    }

    [Fact]
    public void AClick_IsNotAReply()
    {
        // Arrange
        var listener = new CallerReplyListener();
        var frames = Frames(Line, 5).Concat(Frames(-10d, 1)).Concat(Frames(Line, 5)).ToArray();
        var ends = Feed(listener, frames, _start + Ms(1_000), _start);

        // Act
        var unheard = listener.TryTakeUnheardReply(ends + _wait.Ticks, providerHeardCallerTicks: 0, _wait);

        // Assert
        Assert.False(unheard);
    }

    [Fact]
    public void AQuietLine_IsNotAReply()
    {
        // Arrange
        var listener = new CallerReplyListener();
        var ends = Feed(listener, Frames(Line, 100), _start + Ms(1_000), _start);

        // Act
        var unheard = listener.TryTakeUnheardReply(ends + _wait.Ticks, providerHeardCallerTicks: 0, _wait);

        // Assert
        Assert.False(unheard);
    }

    [Fact]
    public void AReply_IsNotTaken_WhileTheCallerIsStillMakingSound()
    {
        // Arrange
        // Live, a soft "um" was taken for a reply, and the question came just as the caller began their answer.
        var listener = new CallerReplyListener();
        var replyEnds = Feed(listener, Frames(Speech, 10), _start + Ms(1_000), _start);
        var now = replyEnds + _wait.Ticks;

        // More voice, too short yet to be a reply of its own, right before the reply would be taken.
        Feed(listener, Frames(Speech, 3), now - Ms(100), _start);

        // Act
        var unheard = listener.TryTakeUnheardReply(now, providerHeardCallerTicks: 0, _wait);

        // Assert
        Assert.False(unheard);
    }

    [Fact]
    public void ASecondReply_AfterTheFirstWasHeard_CanStillGoUnheard()
    {
        // Arrange
        var listener = new CallerReplyListener();
        var firstStarts = _start + Ms(1_000);
        var firstEnds = Feed(listener, Frames(Speech, 10), firstStarts, _start);
        Assert.False(listener.TryTakeUnheardReply(firstEnds + _wait.Ticks, providerHeardCallerTicks: firstStarts + Ms(300), _wait));

        var secondEnds = Feed(listener, Frames(Speech, 10), firstEnds + Ms(5_000), _start);

        // Act
        var unheard = listener.TryTakeUnheardReply(secondEnds + _wait.Ticks, providerHeardCallerTicks: firstStarts + Ms(300), _wait);

        // Assert
        Assert.True(unheard);
    }

    // Feeds the frames one every 20 ms from the start, and returns when the last one arrived.
    [Fact]
    public void AReply_RecordsWhenItBeganOnTheLine()
    {
        // Arrange
        // What the provider's report of the caller starting is measured against, to show a session running behind.
        var listener = new CallerReplyListener();
        var replyStarts = _start + Ms(1_000);

        // Act
        Feed(listener, Frames(Speech, 10), replyStarts, _start);

        // Assert
        Assert.Equal(replyStarts, listener.LatestReplyStartTicks);
    }

    [Fact]
    public void BeforeAnyReply_NothingHasBegun()
        => Assert.Equal(0, new CallerReplyListener().LatestReplyStartTicks);

    private static long Feed(CallerReplyListener listener, byte[][] frames, long startTicks, long assistantPlaysUntilTicks)
    {
        var now = startTicks;

        for (var i = 0; i < frames.Length; i++)
        {
            now = startTicks + Ms((i + 1) * FrameMilliseconds);
            listener.Hear(frames[i], now, assistantPlaysUntilTicks);
        }

        return now;
    }

    private static long Ms(int milliseconds)
        => milliseconds * TimeSpan.TicksPerMillisecond;

    private static byte[][] Frames(double dbfs, int count)
    {
        var frames = new byte[count][];

        for (var i = 0; i < count; i++)
        {
            frames[i] = Tone(dbfs, i);
        }

        return frames;
    }

    // One 20 ms frame of a 300 Hz tone at the given RMS level, as 16-bit PCM at the realtime rate.
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
}
