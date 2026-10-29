using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed partial class RealtimeVoiceConversationRunnerTests
{
    [Fact]
    public async Task ASupervisorSwitch_ChangesTheLiveSessionsProfile_InTheCallsOwnScopes()
    {
        // Arrange
        var harness = new RealtimeHarness();
        using var handoff = new CancellationTokenSource();
        harness.HandoffRequested = handoff.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var billing = new AIProfile { ItemId = "profile-billing", Name = "billing" };
        harness.ProfileManager.Setup(x => x.FindByIdAsync("profile-billing", It.IsAny<CancellationToken>())).ReturnsAsync(billing);

        var run = harness.RunAsync();

        // The session registers for commands once it holds the call.
        while (!await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.KeysPressed, "1")))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        // Act
        var accepted = await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "profile-billing"));
        var waitedUntil = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (harness.Orchestrator.Switches.Count == 0 && DateTime.UtcNow < waitedUntil)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        await handoff.CancelAsync();
        await run;

        // Assert
        Assert.True(accepted);
        var (request, invocation, conversation) = Assert.Single(harness.Orchestrator.Switches);
        Assert.Same(billing, request.Resource);

        // The switch runs where the call's tools read the call from, not in the supervisor's request.
        Assert.NotNull(invocation);
        Assert.Same(harness.Orchestrator.InvocationScopeAtStart, invocation);
        Assert.Same(billing, conversation?.Profile);
    }

    [Fact]
    public async Task ASwitchToAProfileThatDoesNotExist_LeavesTheSessionAsItIs()
    {
        // Arrange
        var harness = new RealtimeHarness();
        using var handoff = new CancellationTokenSource();
        harness.HandoffRequested = handoff.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();

        while (!await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.KeysPressed, "1")))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        // Act
        await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "missing"));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await handoff.CancelAsync();
        await run;

        // Assert
        Assert.Empty(harness.Orchestrator.Switches);
    }

    private sealed partial class RealtimeHarness
    {
        /// <summary>
        /// Finds the profile a supervisor switches the call to.
        /// </summary>
        public Mock<IAIProfileManager> ProfileManager { get; } = new();
    }

    private sealed partial class RecordingRealtimeOrchestrator
    {
        /// <summary>
        /// The switches asked for, with the invocation and conversation that were current for each.
        /// </summary>
        public List<(RealtimeOrchestrationRequest Request, AIInvocationContext Invocation, AIConversationContext Conversation)> Switches { get; } = [];

        public Task SwitchResourceAsync(IRealtimeConversation conversation, RealtimeOrchestrationRequest request, CancellationToken cancellationToken = default)
        {
            Switches.Add((request, AIInvocationScope.Current, AIConversationScope.Current));

            return Task.CompletedTask;
        }
    }
}
