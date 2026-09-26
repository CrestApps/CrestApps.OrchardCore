using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An inbound call to an entry point that has a phone menu. The menu was saved and validated, but inbound routing
/// never started it: the caller was queued to the entry point's target as though there were no menu at all.
/// </summary>
public sealed partial class InboundVoiceServiceTests
{
    [Fact]
    public async Task AnOpenEntryPointWithAMenu_PlaysTheMenuInsteadOfQueueingTheCaller()
    {
        // Arrange
        var (harness, interaction) = MenuHarness(isOpen: true);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(MenuCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ivr_menu", result.ReasonCode);
        Assert.False(result.Queued);
        Assert.False(result.Routed);
        Assert.Null(interaction.QueueId);
        Assert.Equal("entry-menu", interaction.TechnicalMetadata[EntryPointFlowResolver.EntryPointMetadataKey]);
        harness.QueueService.Verify(
            queueService => queueService.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.AssignmentService.Verify(
            assignment => assignment.AssignNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TheMenu_StartsOnlyAfterTheRoutingCommits()
    {
        // Arrange
        // Answering inside the routing transaction lets the provider's answered event arrive for a call the store
        // does not have yet, which is then taken for a brand-new inbound call.
        var (harness, _) = MenuHarness(isOpen: true);
        var service = harness.CreateService();

        // Act
        await service.HandleInboundAsync(MenuCall(), TestContext.Current.CancellationToken);

        // Assert
        harness.IvrRouter.Verify(router => router.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(harness.ScopeExecutor.ScheduledOperation);

        await harness.ScopeExecutor.ScheduledOperation();

        harness.IvrRouter.Verify(router => router.StartAsync("int1", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task AClosedEntryPointWithAMenu_AppliesItsClosedActionInstead()
    {
        // Arrange
        // Business hours decide first: an after-hours caller gets what the entry point does after hours, not a
        // menu of teams that have gone home.
        var (harness, interaction) = MenuHarness(isOpen: false);
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(MenuCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("entry_point_closed_voicemail", result.ReasonCode);
        Assert.False(interaction.TechnicalMetadata.ContainsKey(EntryPointFlowResolver.EntryPointMetadataKey));
        harness.IvrRouter.Verify(router => router.StartAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AMenuWhoseRootIsMissing_RoutesTheWayTheEntryPointAlwaysDid()
    {
        // Arrange
        var (harness, interaction) = MenuHarness(isOpen: true, rootNodeId: "missing");
        var queue = new ActivityQueue { ItemId = "q-main", Enabled = true };
        harness.QueueManager
            .Setup(manager => manager.FindByIdAsync("q-main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queue);
        harness.QueueService
            .Setup(queueService => queueService.EnqueueAsync("act1", "q-main", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi1", ActivityItemId = "act1", QueueId = "q-main" });
        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(MenuCall(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Queued, result.Reason);
        Assert.Equal("q-main", interaction.QueueId);
    }

    private static InboundVoiceEvent MenuCall()
        => new()
        {
            ProviderName = "TestProvider",
            ProviderCallId = "call-1",
            FromAddress = "+15551112222",
            ToAddress = "+15553334444",
        };

    private static (Harness Harness, Interaction Interaction) MenuHarness(bool isOpen, string rootNodeId = "root")
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

        var entryPoint = new ContactCenterEntryPoint
        {
            ItemId = "entry-menu",
            TargetType = EntryPointTargetType.Queue,
            TargetQueueId = "q-main",
            ClosedAction = EntryPointClosedAction.Voicemail,
            IvrFlow = new IvrFlow
            {
                RootNodeId = rootNodeId,
                Nodes = [new IvrNode { NodeId = "root", Prompt = "Press 1 for sales." }],
            },
        };

        harness.EntryPointResolver
            .Setup(resolver => resolver.ResolveAsync("+15553334444", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntryPointRoutingPlanner.CreatePlan(entryPoint, isOpen));

        return (harness, interaction);
    }
}
