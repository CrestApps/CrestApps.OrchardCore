using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What the assistant has queued on the line, so that a caller who talks over it stops hearing it, and the model
/// is told how much of its line was actually heard.
/// </summary>
/// <remarks>
/// Live, the provider stopped the model the moment the caller started talking, and the caller heard the rest of
/// the sentence anyway: the model had already produced it, faster than it plays, and it was waiting at the carrier.
/// These pin down the bookkeeping that takes it back.
/// </remarks>
public sealed class AssistantBargeInTests
{
    private static readonly long _start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    [Fact]
    public void TheLineBeingPlayed_IsCutAtWhatTheCallerHadHeard()
    {
        // Arrange
        // Five seconds of speech handed over at once, as the model does, and the caller talks over it 1.2 seconds in.
        var bargeIn = new AssistantBargeIn();
        bargeIn.Queued("response-1", "item-1", startsTicks: _start, Bytes(5_000), nowTicks: _start);

        // Act
        var truncations = bargeIn.Interrupt(_start + Ms(1_200));

        // Assert
        var truncation = Assert.Single(truncations);
        Assert.Equal("item-1", truncation.ItemId);
        Assert.Equal(1_200, truncation.AudioEndMilliseconds);
    }

    [Fact]
    public void ALineDeliveredInPieces_IsCutAtItsPositionInTheLine_NotAtTheWallClock()
    {
        // Arrange
        // The second piece of the line arrived after the first had finished, so the line went quiet in between.
        // The model's context is its own audio, so the cut is measured along that audio, not along the clock.
        var bargeIn = new AssistantBargeIn();
        bargeIn.Queued("response-1", "item-1", startsTicks: _start, Bytes(1_000), nowTicks: _start);
        bargeIn.Queued("response-1", "item-1", startsTicks: _start + Ms(1_500), Bytes(2_000), nowTicks: _start + Ms(1_500));

        // Act
        var truncations = bargeIn.Interrupt(_start + Ms(2_000));

        // Assert
        Assert.Equal(1_500, Assert.Single(truncations).AudioEndMilliseconds);
    }

    [Fact]
    public void ALineQueuedBehindTheOneBeingPlayed_IsCutBeforeItsFirstWord()
    {
        // Arrange
        var bargeIn = new AssistantBargeIn();
        bargeIn.Queued("response-1", "item-1", startsTicks: _start, Bytes(1_000), nowTicks: _start);
        bargeIn.Queued("response-2", "item-2", startsTicks: _start + Ms(1_000), Bytes(1_000), nowTicks: _start);

        // Act
        var truncations = bargeIn.Interrupt(_start + Ms(400));

        // Assert
        Assert.Equal(
            [new AssistantAudioTruncation("item-1", 400, 1_000), new AssistantAudioTruncation("item-2", 0, 1_000)],
            truncations);
    }

    [Fact]
    public void ALineTheCallerHasAlreadyHeardInFull_IsLeftAlone()
    {
        // Arrange
        var bargeIn = new AssistantBargeIn();
        bargeIn.Queued("response-1", "item-1", startsTicks: _start, Bytes(1_000), nowTicks: _start);
        bargeIn.Queued("response-2", "item-2", startsTicks: _start + Ms(1_000), Bytes(1_000), nowTicks: _start + Ms(900));

        // Act
        var truncations = bargeIn.Interrupt(_start + Ms(1_300));

        // Assert
        Assert.Equal([new AssistantAudioTruncation("item-2", 300, 1_000)], truncations);
    }

    [Fact]
    public void WithNothingLeftToPlay_NothingIsCut()
    {
        // Arrange
        var bargeIn = new AssistantBargeIn();
        bargeIn.Queued("response-1", "item-1", startsTicks: _start, Bytes(1_000), nowTicks: _start);

        // Act
        var truncations = bargeIn.Interrupt(_start + Ms(1_000));

        // Assert
        Assert.Empty(truncations);
        Assert.True(bargeIn.ShouldPlay("response-2", "item-2"));
    }

    [Fact]
    public void TheRestOfALineTheCallerTalkedOver_IsNotPlayed_ButTheNextOneIs()
    {
        // Arrange
        // The model's speech arrives in pieces. Taking back what was queued and then playing the next piece of the
        // same line would restart it mid-sentence, after the caller had already been told it stopped.
        var bargeIn = new AssistantBargeIn();
        bargeIn.Queued("response-1", "item-1", startsTicks: _start, Bytes(2_000), nowTicks: _start);

        // Act
        bargeIn.Interrupt(_start + Ms(500));

        // Assert
        Assert.False(bargeIn.ShouldPlay("response-1", "item-1"));
        Assert.True(bargeIn.ShouldPlay("response-2", "item-2"));
    }

    [Fact]
    public void AProviderThatNamesNothing_StillHasTheRestOfTheLineHeldBack_UntilTheNextTurn()
    {
        // Arrange
        var bargeIn = new AssistantBargeIn();
        bargeIn.Queued(null, null, startsTicks: _start, Bytes(2_000), nowTicks: _start);

        // Act
        var truncations = bargeIn.Interrupt(_start + Ms(500));

        // Assert
        // Nothing can be truncated without a name for it, but the rest must still not play.
        Assert.Empty(truncations);
        Assert.False(bargeIn.ShouldPlay(null, null));

        // And the caller's answer is heard once the model starts its next turn.
        bargeIn.NextTurn();
        Assert.True(bargeIn.ShouldPlay(null, null));
    }

    [Fact]
    public void TheCallerIsTalkingOverTheAssistant_OnlyWhileTheGuardHasJustLetThemThrough()
    {
        // Arrange
        var bargeIn = new AssistantBargeIn();
        var window = Ms(AssistantBargeIn.CallerTalkingOverWindowMilliseconds);

        // Act & Assert
        // Never let through: the speech the provider heard can only have been the assistant's own echo.
        Assert.False(bargeIn.IsCallerTalkingOver(_start));

        bargeIn.CallerTalkingOver(_start);

        // The provider reports speech a little after the audio that caused it.
        Assert.True(bargeIn.IsCallerTalkingOver(_start + window));

        // Long after, a new speech start is not this caller's interruption any more.
        Assert.False(bargeIn.IsCallerTalkingOver(_start + window + Ms(1)));
    }

    [Fact]
    public void ACut_IsNeverPastTheEndOfWhatWasDeliveredForTheLine()
    {
        // Arrange
        // A line delivered in two pieces with a gap between them, interrupted near the end of the second.
        var bargeIn = new AssistantBargeIn();
        bargeIn.Queued("response-1", "item-1", startsTicks: _start, Bytes(3_000), nowTicks: _start);
        bargeIn.Queued("response-1", "item-1", startsTicks: _start + Ms(3_500), Bytes(2_150), nowTicks: _start + Ms(3_500));

        // Act
        var truncation = Assert.Single(bargeIn.Interrupt(_start + Ms(5_600)));

        // Assert
        Assert.Equal(5_150, truncation.DeliveredMilliseconds);
        Assert.InRange(truncation.AudioEndMilliseconds, 0, truncation.DeliveredMilliseconds);
    }

    [Fact]
    public void ACutTheProviderRefused_IsHandedBackOnce_ByTheLengthItAskedFor()
    {
        // Arrange
        // The refusal names only the two lengths ("Audio content of 5150ms is already shorter than 5300ms"), so
        // the cut is found by what it asked for. Handed back once, so a correction refused again is not retried.
        var bargeIn = new AssistantBargeIn();
        bargeIn.Queued("response-1", "item-1", startsTicks: _start, Bytes(1_000), nowTicks: _start);
        var sent = Assert.Single(bargeIn.Interrupt(_start + Ms(400)));

        // Act & Assert
        Assert.False(bargeIn.TryTakeRefused(sent.AudioEndMilliseconds + 1, out _));
        Assert.True(bargeIn.TryTakeRefused(sent.AudioEndMilliseconds, out var refused));
        Assert.Equal(sent, refused);
        Assert.False(bargeIn.TryTakeRefused(sent.AudioEndMilliseconds, out _));
    }

    private static int Bytes(int milliseconds)
        => RealtimeAudioConverter.RealtimeSampleRate * 2 * milliseconds / 1000;

    private static long Ms(int milliseconds)
        => TimeSpan.TicksPerMillisecond * milliseconds;
}
