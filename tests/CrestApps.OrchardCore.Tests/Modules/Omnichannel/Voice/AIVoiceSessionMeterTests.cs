using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The measurements an AI voice call is reported with: how long each side spoke, how long neither did, and how long
/// the caller waited to hear the assistant. They are measured from the audio -- what was played to the caller and
/// when the caller's voice was detected -- rather than from transcript timestamps, which only exist once a turn is
/// over and say nothing about how long it took to say.
/// </summary>
public sealed class AIVoiceSessionMeterTests
{
    private static readonly long _answered = new DateTime(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc).Ticks;

    [Fact]
    public void AConversation_IsMeasuredFromWhatEachSideActuallySaid()
    {
        // Arrange
        // Answered, the assistant speaks for 3 s after a 1 s setup, the caller answers for 2 s after a 1 s pause,
        // and the call ends 2 s after that: 9 s in all, 4 of them silent.
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
        meter.Start(_answered);
        meter.AssistantAudioScheduled(At(1_000), Ms(3_000));
        meter.CallerSpeechStarted(At(5_000));
        meter.CallerSpeechStopped(At(7_000));
        meter.Stop(At(9_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Equal(9_000, measured.SessionDurationMs);
        Assert.Equal(3_000, measured.AssistantSpeakingMs);
        Assert.Equal(2_000, measured.CallerSpeakingMs);
        Assert.Equal(4_000, measured.MutualSilenceMs);
        Assert.Equal(1_000, measured.TimeToFirstAssistantAudioMs);
        Assert.Equal(new DateTime(_answered, DateTimeKind.Utc), measured.StartedUtc);
        Assert.Equal(new DateTime(At(9_000), DateTimeKind.Utc), measured.EndedUtc);
    }

    [Fact]
    public void AudioDeliveredInPieces_IsCountedOnce_AsTheLineItPlaysAs()
    {
        // Arrange
        // The model hands its speech over faster than it plays, so pieces are scheduled back to back. Summing the
        // pieces is right; counting the overlap between two pieces that were scheduled over each other is not.
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
        meter.Start(_answered);
        meter.AssistantAudioScheduled(At(0), Ms(1_000));
        meter.AssistantAudioScheduled(At(1_000), Ms(1_000));
        meter.AssistantAudioScheduled(At(1_500), Ms(1_000));
        meter.Stop(At(10_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Equal(2_500, measured.AssistantSpeakingMs);
    }

    [Fact]
    public void WhenBothSpeakAtOnce_TheOverlapIsNotCountedAsSilence_AndNotTwiceAsTalk()
    {
        // Arrange
        // The caller talks over the last second of the assistant's line. The line is not silent for that second,
        // and it was not 2 seconds of conversation either.
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
        meter.Start(_answered);
        meter.AssistantAudioScheduled(At(0), Ms(3_000));
        meter.CallerSpeechStarted(At(2_000));
        meter.CallerSpeechStopped(At(4_000));
        meter.Stop(At(5_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Equal(3_000, measured.AssistantSpeakingMs);
        Assert.Equal(2_000, measured.CallerSpeakingMs);
        Assert.Equal(1_000, measured.MutualSilenceMs);
    }

    [Fact]
    public void ABargeIn_StopsCountingTheLineTheCallerNeverHeard_AndIsCounted()
    {
        // Arrange
        // Five seconds were queued; the caller cut in at two. The last three were cleared off the line and never
        // played, so they are not talk time.
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
        meter.Start(_answered);
        meter.AssistantAudioScheduled(At(0), Ms(5_000));
        meter.AssistantInterrupted(At(2_000));
        meter.Stop(At(6_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Equal(2_000, measured.AssistantSpeakingMs);
        Assert.Equal(1, measured.BargeIns);
    }

    [Fact]
    public void AudioStillQueuedWhenTheCallEnds_IsNotCountedPastTheEnd()
    {
        // Arrange
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
        meter.Start(_answered);
        meter.AssistantAudioScheduled(At(1_000), Ms(5_000));
        meter.CallerSpeechStarted(At(2_000));
        meter.Stop(At(3_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Equal(2_000, measured.AssistantSpeakingMs);

        // The caller was still talking when the line dropped.
        Assert.Equal(1_000, measured.CallerSpeakingMs);
    }

    [Fact]
    public void AnEngineThatCannotHearTheCallerStartAndStop_ReportsCallerTimeAndSilenceAsUnknown()
    {
        // Arrange
        // A turn-based call only learns what the caller said once it is transcribed. Zero would claim the caller
        // never spoke; unknown is the truth.
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: false);
        meter.Start(_answered);
        meter.AssistantSpeechStarted(At(500));
        meter.AssistantSpeechEnded(At(2_500));
        meter.Stop(At(8_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Equal(2_000, measured.AssistantSpeakingMs);
        Assert.Equal(500, measured.TimeToFirstAssistantAudioMs);
        Assert.Null(measured.CallerSpeakingMs);
        Assert.Null(measured.MutualSilenceMs);
    }

    [Fact]
    public void SpeechThatEndedWithoutItsStartBeingSeen_LeavesTheAssistantTimeUnknown_RatherThanZero()
    {
        // Arrange
        // The provider reported the line finishing but never reported it starting.
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: false);
        meter.Start(_answered);
        meter.AssistantSpeechEnded(At(2_500));
        meter.Stop(At(8_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Null(measured.AssistantSpeakingMs);
        Assert.Null(measured.TimeToFirstAssistantAudioMs);
    }

    [Fact]
    public void ARepeatedStart_DoesNotRestartTheLineOrTheCallerTurn()
    {
        // Arrange
        // Providers report the same moment twice more often than one would like.
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
        meter.Start(_answered);
        meter.Start(At(5_000));
        meter.AssistantSpeechStarted(At(1_000));
        meter.AssistantSpeechStarted(At(2_000));
        meter.AssistantSpeechEnded(At(3_000));
        meter.CallerSpeechStarted(At(4_000));
        meter.CallerSpeechStarted(At(5_000));
        meter.CallerSpeechStopped(At(6_000));
        meter.CallerSpeechStopped(At(7_000));
        meter.Stop(At(10_000));
        meter.Stop(At(20_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Equal(10_000, measured.SessionDurationMs);
        Assert.Equal(2_000, measured.AssistantSpeakingMs);
        Assert.Equal(2_000, measured.CallerSpeakingMs);
    }

    [Fact]
    public void IdlePromptsAndHeldEcho_AreCarriedThrough()
    {
        // Arrange
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
        meter.Start(_answered);
        meter.IdlePrompt();
        meter.IdlePrompt();
        meter.EchoHeld(340);
        meter.Stop(At(1_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Equal(2, measured.IdlePrompts);
        Assert.Equal(340, measured.EchoHeldMs);
        Assert.Equal(0, measured.BargeIns);
    }

    [Fact]
    public void AnEngineWithNoEchoGuard_ReportsHeldEchoAsUnknown()
    {
        // Arrange
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: false);
        meter.Start(_answered);
        meter.Stop(At(1_000));

        // Act
        var measured = meter.Measure();

        // Assert
        Assert.Null(measured.EchoHeldMs);
    }

    [Fact]
    public void AMeterThatNeverStarted_HasNothingToReport()
    {
        // Arrange
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
        meter.AssistantAudioScheduled(At(0), Ms(1_000));

        // Act & Assert
        Assert.Null(meter.Measure());
    }

    [Fact]
    public void AFailure_IsRemembered()
    {
        // Arrange
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: true);
        meter.Start(_answered);

        // Act
        meter.Failed();

        // Assert
        Assert.True(meter.HasFailed);
    }

    [Fact]
    public void AMeterStillRunning_IsMeasuredUpToTheMomentItIsAskedAbout()
    {
        // Arrange
        // A turn-based call is summarized when the provider reports it over, which may be before anything stopped it.
        var meter = new AIVoiceSessionMeter(measuresCallerSpeech: false);
        meter.Start(_answered);
        meter.AssistantSpeechStarted(At(1_000));

        // Act
        var measured = meter.Measure(endTicks: At(4_000));

        // Assert
        Assert.Equal(4_000, measured.SessionDurationMs);
        Assert.Equal(3_000, measured.AssistantSpeakingMs);
    }

    private static long At(int milliseconds)
        => _answered + Ms(milliseconds);

    private static long Ms(int milliseconds)
        => milliseconds * TimeSpan.TicksPerMillisecond;
}
