using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A turn-based call is a string of provider events with nothing held between them, so what is measured across
/// them is kept here until the call ends.
/// </summary>
public sealed class AIVoiceSessionTrackerTests
{
    private static readonly DateTime _answered = new(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ATurnBasedCall_IsMeasuredFromTheProviderSpeechEvents()
    {
        // Arrange
        var tracker = CreateTracker();
        tracker.BeginTurnBased("activity-1", _answered);

        // Act
        tracker.TurnBasedSpeech("activity-1", started: true, _answered.AddSeconds(1));
        tracker.TurnBasedSpeech("activity-1", started: false, _answered.AddSeconds(4));
        tracker.TurnBasedSpeech("activity-1", started: true, _answered.AddSeconds(10));
        tracker.TurnBasedSpeech("activity-1", started: false, _answered.AddSeconds(12));
        var measured = tracker.EndTurnBased("activity-1", _answered.AddSeconds(15));

        // Assert
        Assert.Equal(15_000, measured.SessionDurationMs);
        Assert.Equal(5_000, measured.AssistantSpeakingMs);
        Assert.Equal(1_000, measured.TimeToFirstAssistantAudioMs);
        Assert.Null(measured.CallerSpeakingMs);
    }

    [Fact]
    public void ACallThisNodeNeverSawAnswered_HasNoMeasurement()
    {
        // Arrange
        var tracker = CreateTracker();

        // Act
        tracker.TurnBasedSpeech("activity-1", started: true, _answered);

        // Assert
        Assert.Null(tracker.EndTurnBased("activity-1", _answered.AddSeconds(5)));
    }

    [Fact]
    public void ACallIsEndedOnce()
    {
        // Arrange
        // A handed-off call ends at the handoff, and the provider's hangup for it arrives minutes later.
        var tracker = CreateTracker();
        tracker.BeginTurnBased("activity-1", _answered);

        // Act
        var first = tracker.EndTurnBased("activity-1", _answered.AddSeconds(5));
        var second = tracker.EndTurnBased("activity-1", _answered.AddSeconds(500));

        // Assert
        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void CallsAreKeptApart()
    {
        // Arrange
        var tracker = CreateTracker();
        tracker.BeginTurnBased("activity-1", _answered);
        tracker.BeginTurnBased("activity-2", _answered);

        // Act
        tracker.TurnBasedSpeech("activity-2", started: true, _answered.AddSeconds(1));
        tracker.TurnBasedSpeech("activity-2", started: false, _answered.AddSeconds(3));

        // Assert
        Assert.Equal(0, tracker.EndTurnBased("activity-1", _answered.AddSeconds(5)).AssistantSpeakingMs);
        Assert.Equal(2_000, tracker.EndTurnBased("activity-2", _answered.AddSeconds(5)).AssistantSpeakingMs);
    }

    private static AIVoiceSessionTracker CreateTracker()
        => new(shellHost: null, shellSettings: null, NullLogger<AIVoiceSessionTracker>.Instance);
}
