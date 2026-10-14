using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A realtime call is held by the node it runs on. When the node begins to shut down the session ends as a lost
/// one, and the node waits for the caller to be handed on before it goes.
/// </summary>
public sealed partial class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task ARealtimeSession_IsGivenTheNodesStoppingSignal()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var session = Assert.Single(harness.Realtime.Sessions);
        Assert.Equal(harness.CallShutdown.Stopping, session.NodeStopping);
    }

    [Fact]
    public async Task ARealtimeCall_IsHeldForShutdown_UntilItHasBeenHandedOn()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.UseRealtime();
        var heldDuringSession = 0;
        harness.Realtime.DuringSession = context =>
        {
            heldDuringSession = harness.CallShutdown.LiveCalls;
            context.SessionLost = true;
        };

        // Act
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // Held while the session ran, and let go only once the call had been passed on to be finished.
        Assert.Equal(1, heldDuringSession);
        Assert.NotNull(harness.CompletionRunner.Completion);
        Assert.Equal(0, harness.CallShutdown.LiveCalls);
    }

    [Fact]
    public async Task ASessionEndedByShutdown_HandsTheCallerToAnAgent_WhenTheCallCanReachOne()
    {
        // Arrange
        var harness = new LoopHarness();
        harness.EnableHandoff();
        harness.UseRealtime();
        harness.Realtime.DuringSession = context =>
        {
            // What the runner does when the node begins to stop.
            context.EndedByShutdown = true;
            context.SessionLost = true;
        };
        await harness.HandleAsync(VoiceAgentEventKind.Answered, cancellationToken: TestContext.Current.CancellationToken);

        // Act
        await harness.FinishAsync();

        // Assert
        harness.HandoffService.Verify(
            x => x.RequestHandoffAsync(It.IsAny<OmnichannelHandoffRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(0, harness.Media.Hangups);
    }
}
