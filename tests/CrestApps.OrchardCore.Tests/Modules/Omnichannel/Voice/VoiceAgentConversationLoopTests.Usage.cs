using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What the loop hands over for the AI usage report: one summary per call, from whichever engine held it, with
/// the measurements that engine could take and the deployment that actually held the call.
/// </summary>
public sealed partial class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task ALiveCall_IsSummarizedAsRealtime_OnTheDeploymentThatHeldIt_WithWhatTheSessionMeasured()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.EndCallTurn.Setup(x => x.EndCallRequested).Returns(true);
        var answered = new DateTime(2026, 9, 24, 15, 0, 0, DateTimeKind.Utc).Ticks;
        harness.Realtime.DuringSession = context =>
        {
            context.Meter.Start(answered);
            context.Meter.AssistantAudioScheduled(answered + TimeSpan.TicksPerSecond, 2 * TimeSpan.TicksPerSecond);
            context.Meter.Stop(answered + (10 * TimeSpan.TicksPerSecond));
        };

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var draft = Assert.Single(harness.SessionTracker.Recorded);
        Assert.Equal(harness.Activity.ItemId, draft.ActivityId);
        Assert.Equal("Fake", draft.ProviderName);
        Assert.Equal("call-1", draft.ProviderCallId);
        Assert.Equal(AIVoiceSessionEngine.Realtime, draft.Engine);
        Assert.Equal("realtime-deployment", draft.DeploymentName);
        Assert.Equal(AIVoiceSessionOutcome.CompletedByAI, draft.Outcome);
        Assert.True(draft.WasAnswered);
        Assert.Equal(10_000, draft.Measurements.SessionDurationMs);
        Assert.Equal(2_000, draft.Measurements.AssistantSpeakingMs);

        // A live call is not measured as a turn-based one as well.
        Assert.Empty(harness.SessionTracker.Begun);
    }

    [Fact]
    public async Task ALiveCallHandedToAPerson_IsSummarizedAsAHandoff()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        harness.Realtime.DuringSession = context => context.Meter.Start(DateTime.UtcNow.Ticks);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AIVoiceSessionOutcome.HandedToAgent, Assert.Single(harness.SessionTracker.Recorded).Outcome);
    }

    [Fact]
    public async Task ALiveSessionThatNeverHeldTheCall_LeavesNoRealtimeSummary_AndTheTurnBasedCallIsMeasuredInstead()
    {
        // Arrange
        // No live media on this provider: the call falls back to the turn-based loop, and that is what it is
        // reported as.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.Realtime.CanRun = false;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.SessionTracker.Recorded);
        Assert.Equal([harness.Activity.ItemId], harness.SessionTracker.Begun);
    }

    [Fact]
    public async Task ATurnBasedCall_IsMeasuredFromWhenTheProviderSaysTheAssistantsSpeechStartedAndEnded()
    {
        // Arrange
        var harness = new LoopHarness();
        var started = new DateTime(2026, 9, 24, 15, 0, 1, DateTimeKind.Utc);
        var ended = started.AddSeconds(3);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechStarted, cancellationToken: TestContext.Current.CancellationToken, occurredUtc: started);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken, occurredUtc: ended);

        // Assert
        Assert.Equal([harness.Activity.ItemId], harness.SessionTracker.Begun);
        Assert.Equal([(harness.Activity.ItemId, true, started), (harness.Activity.ItemId, false, ended)], harness.SessionTracker.Speech);
    }

    [Fact]
    public async Task ASpeechEventWithNoTimeOfItsOwn_IsMeasuredOnTheLoopsClock()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechStarted, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(harness.Clock.UtcNow, Assert.Single(harness.SessionTracker.Speech).OccurredUtc);
    }

    [Fact]
    public async Task ATurnBasedCallHandedToAPerson_IsSummarizedAtTheHandoff_WithWhatWasMeasuredUntilThen()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.Reply = "Let me put you through.";
        harness.HandoffTurn.Setup(x => x.HandoffRequested).Returns(true);
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "Can I speak to a person?", cancellationToken: TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromSeconds(4));
        var handoffUtc = harness.Clock.UtcNow;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var draft = Assert.Single(harness.SessionTracker.Recorded);
        Assert.Equal(AIVoiceSessionEngine.TurnBased, draft.Engine);
        Assert.Equal(AIVoiceSessionOutcome.HandedToAgent, draft.Outcome);
        Assert.True(draft.WasAnswered);
        Assert.NotNull(draft.Measurements);
        Assert.Equal((harness.Activity.ItemId, handoffUtc), Assert.Single(harness.SessionTracker.Ended));
    }

    [Fact]
    public async Task AHangup_SummarizesTheTurnBasedCall_LeavingTheOutcomeToBeReadFromWhatTheCallLeft()
    {
        // Arrange
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        harness.Clock.Advance(TimeSpan.FromSeconds(30));
        var hungUp = harness.Clock.UtcNow;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Hangup, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var draft = Assert.Single(harness.SessionTracker.Recorded);
        Assert.Equal(AIVoiceSessionEngine.TurnBased, draft.Engine);
        Assert.Null(draft.Outcome);
        Assert.True(draft.WasAnswered);
        Assert.Equal(hungUp, draft.EndedUtc);
        Assert.NotNull(draft.Measurements);
    }

    [Fact]
    public async Task AHangupOfACallNobodyAnswered_IsSummarizedAsUnanswered()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.Activity.Status = ActivityStatus.AwaitingCustomerAnswer;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Hangup, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var draft = Assert.Single(harness.SessionTracker.Recorded);
        Assert.False(draft.WasAnswered);
        Assert.Null(draft.Measurements);
    }

    /// <summary>
    /// Records what the loop measured and asked to have written.
    /// </summary>
    internal sealed class RecordingSessionTracker : IAIVoiceSessionTracker
    {
        public List<string> Begun { get; } = [];

        public List<(string ActivityId, bool Started, DateTime OccurredUtc)> Speech { get; } = [];

        public List<(string ActivityId, DateTime EndedUtc)> Ended { get; } = [];

        public List<AIVoiceSessionDraft> Recorded { get; } = [];

        public void BeginTurnBased(string activityId, DateTime answeredUtc)
            => Begun.Add(activityId);

        public void TurnBasedSpeech(string activityId, bool started, DateTime occurredUtc)
            => Speech.Add((activityId, started, occurredUtc));

        public AIVoiceSessionMeasurements EndTurnBased(string activityId, DateTime endedUtc)
        {
            if (!Begun.Contains(activityId) || Ended.Exists(ended => ended.ActivityId == activityId))
            {
                return null;
            }

            Ended.Add((activityId, endedUtc));

            return new AIVoiceSessionMeasurements { EndedUtc = endedUtc };
        }

        public Task RecordAsync(AIVoiceSessionDraft draft)
        {
            Recorded.Add(draft);

            return Task.CompletedTask;
        }
    }
}
