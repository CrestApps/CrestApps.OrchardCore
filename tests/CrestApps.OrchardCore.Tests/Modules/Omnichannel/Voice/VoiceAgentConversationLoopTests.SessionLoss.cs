using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Telephony.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A live session that was lost with the caller still on the line, and could not be brought back.
/// </summary>
/// <remarks>
/// Live, the session ended on a provider error, nothing had been decided — no transfer, no goodbye — and so nothing
/// was done. The caller sat in fifty seconds of silence, gave up, and the call was concluded "Done". Whatever else
/// happens, a caller must never be left holding a silent line.
/// </remarks>
public sealed partial class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task ALostSession_IsHandedOnToBeFinished_ThoughTheModelDecidedNothing()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.Realtime.DuringSession = context => context.SessionLost = true;

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(harness.CompletionRunner.Completion);
        Assert.True(harness.CompletionRunner.Completion.SessionLost);
        Assert.False(harness.CompletionRunner.Completion.HandoffRequested);
    }

    [Fact]
    public async Task ALostSession_OnACallThatCanReachAnAgent_HandsTheCallerToOne()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.Realtime.DuringSession = context => context.SessionLost = true;
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.FinishAsync();

        // Assert
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotEmpty(harness.Media.Spoken);
        Assert.Equal(0, harness.Media.Hangups);
    }

    [Fact]
    public async Task ALostSession_WithNobodyToHandTo_ApologizesAndThenEndsTheCall()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.Realtime.DuringSession = context => context.SessionLost = true;
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.FinishAsync();

        // Assert
        // Something is said straight away, so the line is not silent while the call is wound up.
        var apology = Assert.Single(harness.Media.Spoken);
        Assert.Contains("sorry", apology, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, harness.Media.Hangups);

        // Once it has been said, the call is hung up rather than left open.
        await harness.HandleAsync(VoiceAgentEventKind.SpeechEnded, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, harness.Media.Hangups);
    }

    [Fact]
    public async Task ALostSession_IsRecordedOnTheActivity_SoTheCallIsNotConcludedAsFinished()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.Realtime.DuringSession = context => context.SessionLost = true;
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.FinishAsync();

        // Assert
        Assert.True(harness.Activity.TryGet<AIVoiceSessionLost>(out var lost));
        Assert.NotNull(lost.LostUtc);
    }

    [Fact]
    public async Task ASessionThatEndedWithAGoodbye_IsNotTreatedAsLost()
    {
        // Arrange
        // Both at once: the model said goodbye and asked to end the call, then the provider dropped the session.
        // The conversation had finished, so the call just ends; nothing about it failed.
        var harness = new LoopHarness();
        harness.UseRealtime();
        harness.EndCallTurn.Setup(x => x.EndCallRequested).Returns(true);
        harness.Realtime.DuringSession = context => context.SessionLost = true;
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.FinishAsync();

        // Assert
        Assert.Equal(1, harness.Media.Hangups);
        Assert.Empty(harness.Media.Spoken);
        Assert.False(harness.Activity.TryGet<AIVoiceSessionLost>(out _));
    }
}
