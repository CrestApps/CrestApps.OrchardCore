using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Concluding a call whose end the provider never reported.
/// </summary>
/// <remarks>
/// Live, a hangup that arrived while the database was locked was dropped, and its activity stayed in progress for
/// good: nothing else concludes an automated call, and every later load skipped the lead as already being worked.
/// </remarks>
public sealed partial class VoiceAgentConversationLoopTests
{
    [Fact]
    public async Task AStrandedCall_HandedToAPerson_IsLeftToThem()
    {
        var harness = new LoopHarness();
        harness.Activity.Status = ActivityStatus.InProgress;
        harness.Activity.AiEscalated = true;

        var concluded = await harness.Loop.ConcludeStrandedCallAsync(harness.Activity.ItemId, TestContext.Current.CancellationToken);

        Assert.False(concluded);
        Assert.Equal(ActivityStatus.InProgress, harness.Activity.Status);
    }

    [Fact]
    public async Task AStrandedCall_ThatWasConcludedMeanwhile_IsLeftAlone()
    {
        var harness = new LoopHarness();
        harness.Activity.Status = ActivityStatus.Completed;

        var concluded = await harness.Loop.ConcludeStrandedCallAsync(harness.Activity.ItemId, TestContext.Current.CancellationToken);

        Assert.False(concluded);
    }

    [Fact]
    public async Task AStrandedCall_WithNoConversationToReview_IsFailed_SoItIsNotFoundAgain()
    {
        var harness = new LoopHarness();
        harness.Activity.Status = ActivityStatus.AwaitingCustomerAnswer;
        harness.Activity.AISessionId = null;

        var concluded = await harness.Loop.ConcludeStrandedCallAsync(harness.Activity.ItemId, TestContext.Current.CancellationToken);

        Assert.True(concluded);
        Assert.Equal(ActivityStatus.Failed, harness.Activity.Status);
        Assert.False(string.IsNullOrEmpty(harness.Activity.TerminalReasonCode));
        Assert.NotNull(harness.Activity.CompletedUtc);
    }

    [Fact]
    public async Task AStrandedCall_WithAConversation_IsConcludedTheWayItsHangupWouldHaveBeen()
    {
        var harness = new LoopHarness();
        harness.Activity.Status = ActivityStatus.InProgress;
        harness.Activity.AISessionId = "session-1";

        var concluded = await harness.Loop.ConcludeStrandedCallAsync(harness.Activity.ItemId, TestContext.Current.CancellationToken);

        // The review itself runs after the scope commits, exactly as it does for a reported hangup.
        Assert.True(concluded);
        Assert.Equal(ActivityStatus.InProgress, harness.Activity.Status);
    }
}
