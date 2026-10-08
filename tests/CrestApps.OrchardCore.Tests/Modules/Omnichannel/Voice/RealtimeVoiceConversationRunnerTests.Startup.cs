namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// How quickly a call gets to its greeting.
/// </summary>
/// <remarks>
/// Live, the caller answered with "hello?" and heard the greeting four and a half seconds later: the provider's
/// audio stream was connected first, and only then was the model's session opened.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunnerTests
{
    [Fact]
    public async Task TheModelsSession_StartsOpening_WhileTheCallsAudioIsStillConnecting()
    {
        // Arrange
        var harness = new RealtimeHarness { MediaConnecting = new TaskCompletionSource() };

        // Act
        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Orchestrator.Starts == 1);

        // Assert
        Assert.False(harness.MediaConnecting.Task.IsCompleted);

        harness.MediaConnecting.SetResult();
        await LetTheCallGoAsync(harness, run);
        Assert.True(run.IsCompleted);
    }

    [Fact]
    public async Task ASessionThatCannotOpen_ClosesTheAudioStreamThatOpenedBesideIt()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Orchestrator.Fail = true;

        // Act
        var heldTheCall = await harness.RunAsync();

        // Assert
        Assert.False(heldTheCall);
        Assert.True(harness.Media.Disposed);
    }

    [Fact]
    public async Task AnAudioStreamThatCannotOpen_ClosesTheSessionThatOpenedBesideIt()
    {
        // Arrange
        var harness = new RealtimeHarness { MediaFails = true };

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(harness.RunAsync);

        // Assert
        Assert.True(harness.Conversation.Disposed);
    }
}
