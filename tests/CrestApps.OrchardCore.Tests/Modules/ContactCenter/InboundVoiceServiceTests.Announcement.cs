using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Which of an entry point's messages an inbound caller is owed, and where they go once it has been said. The welcome
/// and closed messages were saved on the entry point and never spoken: an open line went straight to its menu or
/// queue, and a closed one applied its closed action in silence, so an after-hours caller was dropped or sent to
/// voicemail without being told why.
/// </summary>
public sealed partial class InboundVoiceServiceTests
{
    [Fact]
    public async Task AnOpenEntryPointWithAMenuAndAWelcome_OwesTheWelcome_ThenTheMenu()
    {
        // Arrange
        var (harness, interaction) = AnnouncementHarness(isOpen: true, welcomeMessage: "Thanks for calling.", withMenu: true);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("entry_point_welcome", result.ReasonCode);
        AssertOwed(interaction, EntryPointAnnouncement.Welcome, EntryPointAnnouncement.NextMenu);

        // The menu has not started, so the caller's key presses are still menu choices rather than a queue's offer.
        Assert.False(interaction.TechnicalMetadata.ContainsKey(IvrExecutionService.StateMetadataKey));
        AssertNotRoutedYet(harness);
    }

    [Fact]
    public async Task AnOpenEntryPointWithNoMenu_OwesTheWelcome_ThenItsQueue()
    {
        // Arrange
        // Admitting the caller here would queue them, and play the queue's music, over the welcome they are hearing.
        var (harness, interaction) = AnnouncementHarness(isOpen: true, welcomeMessage: "Thanks for calling.");
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("entry_point_welcome", result.ReasonCode);
        AssertOwed(interaction, EntryPointAnnouncement.Welcome, EntryPointAnnouncement.NextTarget);
        Assert.True(IvrExecutionService.ReadState(interaction).Completed);
        AssertNotRoutedYet(harness);
    }

    [Fact]
    public async Task AnOpenPersonalLine_OwesTheWelcome_ThenItsAgent()
    {
        // Arrange
        var (harness, interaction) = AnnouncementHarness(isOpen: true, welcomeMessage: "You've reached Sam.", personalLine: true);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("entry_point_welcome", result.ReasonCode);
        AssertOwed(interaction, EntryPointAnnouncement.Welcome, EntryPointAnnouncement.NextTarget);
        AssertNotRoutedYet(harness);
    }

    [Theory]
    [InlineData(EntryPointClosedAction.HoldInQueue, "q-main")]
    [InlineData(EntryPointClosedAction.Overflow, "q-overflow")]
    public async Task AClosedEntryPointThatKeepsCallers_OwesTheClosedMessage_ThenTheQueueItKeepsThemIn(EntryPointClosedAction closedAction, string queueId)
    {
        // Arrange
        var (harness, interaction) = AnnouncementHarness(isOpen: false, closedMessage: "We are closed.", closedAction: closedAction);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("entry_point_closed_message", result.ReasonCode);
        AssertOwed(interaction, EntryPointAnnouncement.Closed, EntryPointAnnouncement.NextQueue);
        Assert.Equal(queueId, EntryPointAnnouncement.Read(interaction).QueueId);
        Assert.True(IvrExecutionService.ReadState(interaction).Completed);
        AssertNotRoutedYet(harness);
    }

    [Theory]
    [InlineData(EntryPointClosedAction.Voicemail, EntryPointAnnouncement.NextVoicemail)]
    [InlineData(EntryPointClosedAction.Reject, EntryPointAnnouncement.NextReject)]
    public async Task AClosedEntryPointThatTurnsCallersAway_OwesTheClosedMessage_BeforeTheCallIsEnded(EntryPointClosedAction closedAction, string next)
    {
        // Arrange
        // Ending the call (voicemail or reject) during routing would cut the caller off before they heard why.
        var (harness, interaction) = AnnouncementHarness(isOpen: false, closedMessage: "We are closed.", closedAction: closedAction);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("entry_point_closed_message", result.ReasonCode);
        AssertOwed(interaction, EntryPointAnnouncement.Closed, next);
        Assert.False(interaction.IsSettled);
        AssertNotRoutedYet(harness);
    }

    [Fact]
    public async Task TheMessage_IsSaidOnlyAfterTheRoutingCommits()
    {
        // Arrange
        // Answering and speaking inside the routing transaction lets the provider's events for the call arrive for an
        // interaction the store does not have yet.
        var (harness, _) = AnnouncementHarness(isOpen: true, welcomeMessage: "Thanks for calling.", withMenu: true);
        var service = harness.CreateService();

        // Act
        await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        harness.IvrRouter.Verify(router => router.AnnounceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(harness.ScopeExecutor.ScheduledOperation);

        await harness.ScopeExecutor.ScheduledOperation();

        harness.IvrRouter.Verify(router => router.AnnounceAsync("int1", CancellationToken.None), Times.Once);

        // The menu is started by the end of the welcome, never alongside it.
        harness.IvrRouter.Verify(router => router.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnOpenEntryPointWithNoWelcome_PlaysItsMenuAsBefore(string welcomeMessage)
    {
        // Arrange
        // A closed message alone is not a welcome.
        var (harness, interaction) = AnnouncementHarness(isOpen: true, welcomeMessage: welcomeMessage, closedMessage: "We are closed.", withMenu: true);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ivr_menu", result.ReasonCode);
        Assert.Null(EntryPointAnnouncement.Read(interaction));
        await harness.ScopeExecutor.ScheduledOperation();
        harness.IvrRouter.Verify(router => router.StartAsync("int1", CancellationToken.None), Times.Once);
        harness.IvrRouter.Verify(router => router.AnnounceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnOpenEntryPointWithNoWelcomeOrMenu_QueuesTheCallerAsBefore()
    {
        // Arrange
        var (harness, interaction) = AnnouncementHarness(isOpen: true, closedMessage: "We are closed.");
        harness.QueueService
            .Setup(queueService => queueService.EnqueueAsync("act1", "q-main", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi1", ActivityItemId = "act1", QueueId = "q-main" });
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Queued, result.Reason);
        Assert.Null(EntryPointAnnouncement.Read(interaction));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task AClosedEntryPointWithNoClosedMessage_AppliesItsClosedActionAsBefore(string closedMessage)
    {
        // Arrange
        // A welcome alone is not said to a caller who rings after hours.
        var (harness, interaction) = AnnouncementHarness(isOpen: false, welcomeMessage: "Thanks for calling.", closedMessage: closedMessage);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("entry_point_closed_voicemail", result.ReasonCode);
        Assert.Null(EntryPointAnnouncement.Read(interaction));
        harness.ProviderCommandStateService.Verify(
            commands => commands.RegisterAsync(
                It.Is<ProviderCommandRegistration>(registration => registration.CommandType == ProviderCommandType.SendToVoicemail),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnOpenEntryPointWhoseQueueIsGone_SaysNoWelcome_AndTurnsTheCallerAwayAsBefore()
    {
        // Arrange
        // Welcoming a caller who has nowhere to go and then dropping them is worse than turning them away at once.
        var (harness, interaction) = AnnouncementHarness(isOpen: true, welcomeMessage: "Thanks for calling.", queueExists: false);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("target_queue_missing", result.ReasonCode);
        Assert.Null(EntryPointAnnouncement.Read(interaction));
        Assert.False(interaction.TechnicalMetadata.ContainsKey(EntryPointFlowResolver.EntryPointMetadataKey));
    }

    [Theory]
    [InlineData(EntryPointClosedAction.HoldInQueue)]
    [InlineData(EntryPointClosedAction.Overflow)]
    public async Task AClosedEntryPointWhoseHoldingQueueIsGone_SaysNoClosedMessage(EntryPointClosedAction closedAction)
    {
        // Arrange
        var (harness, interaction) = AnnouncementHarness(isOpen: false, closedMessage: "We are closed.", closedAction: closedAction, queueExists: false);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(AnnouncementCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("target_queue_missing", result.ReasonCode);
        Assert.Null(EntryPointAnnouncement.Read(interaction));
        Assert.False(interaction.TechnicalMetadata.ContainsKey(EntryPointFlowResolver.EntryPointMetadataKey));
    }

    private static void AssertOwed(Interaction interaction, string kind, string next)
    {
        var announcement = EntryPointAnnouncement.Read(interaction);

        Assert.NotNull(announcement);
        Assert.Equal(kind, announcement.Kind);
        Assert.Equal(next, announcement.Next);
        Assert.Equal(EntryPointAnnouncement.Scheduled, announcement.Status);

        // The entry point is recorded so the end of the message finds it even if the number is re-pointed meanwhile.
        Assert.Equal("entry-ann", interaction.TechnicalMetadata[EntryPointFlowResolver.EntryPointMetadataKey]);
    }

    // Nothing happens to a caller who is owed a message until it has been said: not admitted, queued, offered or ended.
    private static void AssertNotRoutedYet(Harness harness)
    {
        harness.QueueLimitService.Verify(
            limits => limits.AdmitAsync(It.IsAny<ActivityQueue>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.QueueService.Verify(
            queueService => queueService.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.AssignmentService.Verify(
            assignment => assignment.AssignNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ProviderCommandStateService.Verify(
            commands => commands.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static InboundVoiceEvent AnnouncementCall()
        => new()
        {
            ProviderName = "TestProvider",
            ProviderCallId = "call-1",
            FromAddress = "+15551112222",
            ToAddress = "+15553334444",
        };

    private static (Harness Harness, Interaction Interaction) AnnouncementHarness(
        bool isOpen,
        string welcomeMessage = null,
        string closedMessage = null,
        bool withMenu = false,
        EntryPointClosedAction closedAction = EntryPointClosedAction.Voicemail,
        bool personalLine = false,
        bool queueExists = true)
    {
        var harness = new Harness();
        harness.SetupNoContext();

        var activity = new OmnichannelActivity { ItemId = "act1" };
        var interaction = new Interaction { ItemId = "int1" };

        harness.ActivityManager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        harness.InteractionManager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        if (queueExists)
        {
            foreach (var queueId in new[] { "q-main", "q-overflow" })
            {
                harness.QueueManager
                    .Setup(manager => manager.FindByIdAsync(queueId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ActivityQueue { ItemId = queueId, Enabled = true });
            }
        }

        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "entry-ann",
            TargetType = personalLine ? EntryPointTargetType.Agent : EntryPointTargetType.Queue,
            TargetAgentId = personalLine ? "agent-sam" : null,
            TargetQueueId = personalLine ? null : "q-main",
            OverflowQueueId = "q-overflow",
            ClosedAction = closedAction,
            WelcomeMessage = welcomeMessage,
            ClosedMessage = closedMessage,
            VoicemailEnabled = true,
            IvrFlow = withMenu
                ? new IvrFlow
                {
                    RootNodeId = "root",
                    Nodes = [new IvrNode { NodeId = "root", Prompt = "Press 1 for sales." }],
                }
                : null,
        };

        harness.EntryPointResolver
            .Setup(resolver => resolver.ResolveAsync("+15553334444", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen));

        return (harness, interaction);
    }
}
