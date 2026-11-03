using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.Core;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// How the loop carries the provider's answering-machine reports to a live session holding the call, and finishes a
/// call that session left a message on.
/// </summary>
public sealed partial class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task AMachineTheProviderDetected_ReachesTheLiveSessionHoldingTheCall()
    {
        // Arrange
        // The detection and the greeting's end each arrive on a webhook request of their own while the answered
        // request holds the call; the activity they mark is only read once the call is over.
        var harness = new LoopHarness();
        harness.UseRealtime();

        RealtimeVoiceConversationContext held = null;
        harness.Realtime.DuringSession = context =>
        {
            held = context;
            harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine, cancellationToken: TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();
            harness.HandleAsync(VoiceAgentEventKind.MachineGreetingEnded, cancellationToken: TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();
        };

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(held);
        Assert.True(held.AnsweredByMachine.IsCancellationRequested);
        Assert.True(held.MachineGreetingEnded.IsCancellationRequested);

        // The activity is marked as before, for the conclusion.
        Assert.True(harness.Activity.TryGet<VoicemailReached>(out var voicemail));
        Assert.True(voicemail.DetectedByProvider);
        Assert.True(voicemail.GreetingEnded);
    }

    [Fact]
    public async Task AMachineReportedBeforeTheSessionStarts_IsStillSeenByIt()
    {
        // Arrange
        // Webhooks are not ordered: a quick detection can be processed before the answered event.
        var harness = new LoopHarness();
        harness.UseRealtime();
        await harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var held = Assert.Single(harness.Realtime.Sessions);
        Assert.True(held.AnsweredByMachine.IsCancellationRequested);
        Assert.False(held.MachineGreetingEnded.IsCancellationRequested);
    }

    [Fact]
    public async Task KeysPressedOnTheCall_ReachTheLiveSessionHoldingIt()
    {
        // Arrange
        // The keys are pressed by a tool in the request holding the call, and the session must hear it to stop
        // treating the menu as a voicemail.
        var harness = new LoopHarness();
        harness.UseRealtime();

        RealtimeVoiceConversationContext held = null;
        harness.Realtime.DuringSession = context =>
        {
            held = context;
            harness.AnsweringMachine.PhoneMenuReached("Fake", "call-1");
        };

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(held);
        Assert.True(held.PhoneMenuReached.IsCancellationRequested);
    }

    [Fact]
    public async Task AMachineReportedAfterKeysWerePressed_IsAPhoneMenu_NotAVoicemail()
    {
        // Arrange
        // Live, the assistant pressed the key for the person it was calling three seconds before the provider
        // reported a machine, and the call was then treated as a voicemail.
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        harness.AnsweringMachine.PhoneMenuReached("Fake", "call-1");

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(harness.Activity.TryGet<VoicemailReached>(out _));
    }

    [Fact]
    public async Task WhenTheLiveSessionLeavesItsMessage_TheCallIsEndedOnVoicemail_AndTheActivitySaysSo()
    {
        // Arrange
        var harness = new LoopHarness(useRealTurns: true);
        harness.UseRealtime();
        harness.Realtime.DuringSession = context => context.EndCallOnVoicemail();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // Ended exactly as the end-call tool ends a call with voicemail reported.
        Assert.True(harness.RealEndCallTurn.EndCallRequested);
        Assert.True(harness.RealEndCallTurn.ReachedVoicemail);
        Assert.NotNull(harness.CompletionRunner.Completion);
        Assert.True(harness.CompletionRunner.Completion.EndCallRequested);
        Assert.True(harness.CompletionRunner.Completion.ReachedVoicemail);

        // Finished on a scope of its own: the call is hung up, and the activity records the message as left, so
        // the conclusion reads it as a voicemail however the greeting was transcribed.
        await harness.FinishAsync();

        Assert.Equal(1, harness.Media.Hangups);
        Assert.True(harness.Activity.TryGet<VoicemailReached>(out var voicemail));
        Assert.True(voicemail.MessageLeft);
    }

    [Fact]
    public async Task TheSignalsOfACall_AreForgottenWhenItHangsUp()
    {
        // Arrange
        // The signals live in a tenant singleton, so a call that is over must leave nothing behind in it.
        var harness = new LoopHarness();
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.AnswererDetected, answerer: VoiceAgentAnswerer.Machine, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(harness.AnsweringMachine.Watch("Fake", "call-1").MachineDetected.IsCancellationRequested);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Hangup, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, harness.AnsweringMachine.Count);
        Assert.False(harness.AnsweringMachine.Watch("Fake", "call-1").MachineDetected.IsCancellationRequested);
    }

    [Fact]
    public async Task TheSignalsOfACall_AreForgottenWhenItsLiveSessionEnds()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(harness.Realtime.Sessions);
        Assert.Equal(0, harness.AnsweringMachine.Count);
    }

    [Fact]
    public async Task ATurnBasedVoicemailMessage_IsAskedForWithTheInstructionBothPathsShare()
    {
        // Arrange
        // One instruction for both ways of holding a call, so the two leave the same kind of message.
        var harness = new LoopHarness();
        harness.Reply = "Hi Amani, this is Alex from Contoso Motors. Sorry we missed you, we will try again soon.";
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Transcription, "When you have finished recording you may hang up.", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(VoiceCallGuidance.LeavingAVoicemail, harness.Transcript[^1].Text, StringComparison.Ordinal);
        Assert.Equal(harness.Reply, harness.Media.Spoken[^1]);
    }
}
