namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A live conversation cannot outlive the process it runs in. When the node begins to shut down, the session is
/// ended and reported lost, so whoever holds the call hands the caller on instead of leaving them on a line that
/// falls silent when the process exits.
/// </summary>
public sealed partial class RealtimeVoiceConversationRunnerTests
{
    [Fact]
    public async Task WhenTheNodeBeginsToStop_TheSessionEnds_AndIsReportedLost()
    {
        // Arrange
        // The line and the model session both stay open, as on a live call, so only the shutdown can end it.
        var harness = new RealtimeHarness();
        using var stopping = new CancellationTokenSource();
        harness.NodeStopping = stopping.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();
        await stopping.CancelAsync();
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(run, completed);
        Assert.True(await run);
        Assert.True(harness.LastContext.SessionLost);
        Assert.True(harness.LastContext.EndedByShutdown);
        Assert.True(harness.Media.Stopped);
    }

    [Fact]
    public async Task ACallAnsweredAfterTheNodeBeganToStop_IsHandedOnAtOnce()
    {
        // Arrange
        var harness = new RealtimeHarness();
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();
        harness.NodeStopping = stopping.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(run, completed);
        Assert.True(harness.LastContext.SessionLost);
        Assert.True(harness.LastContext.EndedByShutdown);
    }

    [Fact]
    public async Task ACallThatEndsNormally_IsNotReportedEndedByShutdown()
    {
        // Arrange
        var harness = new RealtimeHarness();
        using var stopping = new CancellationTokenSource();
        harness.NodeStopping = stopping.Token;

        // Act
        await harness.RunAsync();

        // Assert
        Assert.False(harness.LastContext.EndedByShutdown);
        Assert.False(harness.LastContext.SessionLost);
    }
}
