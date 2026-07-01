using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class InboundVoiceServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleInboundAsync_WhenAgentAvailable_CreatesInboundActivityAndOffersAgent()
    {
        // Arrange
        var harness = new Harness();

        harness.ChannelEndpointManager
            .Setup(m => m.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OmnichannelChannelEndpoint>(new OmnichannelChannelEndpoint { ItemId = "ep1", Channel = "Phone", Value = "+15553334444" }));

        harness.SubjectFlowSettingsService
            .Setup(m => m.GetConfiguredFlowSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SubjectFlowSettings { Channel = "Phone", ChannelEndpointId = "ep1", SubjectContentType = "CallSubject", CampaignId = "camp1" }]);

        harness.ContactLookup
            .Setup(m => m.FindContactItemIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["contact1"]);

        harness.ContentManager
            .Setup(m => m.GetAsync("contact1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem { ContentType = "Customer" });

        harness.ContentManager
            .Setup(m => m.NewAsync("CallSubject"))
            .ReturnsAsync(new ContentItem { ContentType = "CallSubject" });

        var activity = new OmnichannelActivity { ItemId = "act1" };
        harness.ActivityManager
            .Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);

        harness.QueueManager
            .Setup(m => m.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActivityQueue { ItemId = "q1", Enabled = true, InboundChannelEndpointId = "ep1" }]);

        var interaction = new Interaction { ItemId = "int1" };
        harness.InteractionManager
            .Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        harness.InteractionManager
            .Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        harness.QueueService
            .Setup(m => m.EnqueueAsync("act1", "q1", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi1", ActivityItemId = "act1", QueueId = "q1" });

        harness.AssignmentService
            .Setup(m => m.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1", AgentId = "a1", ActivityItemId = "act1", QueueId = "q1" });

        harness.AgentManager
            .Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", UserId = "u1" });

        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(
            new InboundVoiceEvent
            {
                ProviderName = "TestProvider",
                ProviderCallId = "call-1",
                FromAddress = "+15551112222",
                ToAddress = "+15553334444",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Routed);
        Assert.Equal("u1", result.AgentUserId);
        Assert.Equal("act1", result.ActivityItemId);
        Assert.Equal("q1", result.QueueId);

        Assert.Equal(ActivityKind.Call, activity.Kind);
        Assert.Equal(ActivitySources.Inbound, activity.Source);
        Assert.Equal("CallSubject", activity.SubjectContentType);
        Assert.Equal(ActivityAssignmentStatus.Available, activity.AssignmentStatus);
        Assert.NotNull(activity.Subject);

        Assert.Equal(InteractionChannel.Voice, interaction.Channel);
        Assert.Equal(InteractionDirection.Inbound, interaction.Direction);

        harness.IncomingCallDispatcher.Verify(
            d => d.DispatchAsync("u1", It.Is<TelephonyCall>(call => call.Direction == CallDirection.Inbound && call.CallId == "call-1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleInboundAsync_WhenNoQueueResolved_DoesNotRoute()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupNoContext();

        harness.QueueManager
            .Setup(m => m.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(
            new InboundVoiceEvent { ProviderName = "TestProvider", ProviderCallId = "call-1", FromAddress = "+15551112222", ToAddress = "+15553334444" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Routed);
        Assert.Null(result.QueueId);
        Assert.Equal("int1", result.InteractionId);
        harness.IncomingCallDispatcher.Verify(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<TelephonyCall>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleInboundAsync_WhenNoAgentAvailable_QueuesWithoutOffering()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupNoContext();

        harness.QueueManager
            .Setup(m => m.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActivityQueue { ItemId = "q1", Enabled = true }]);

        harness.QueueService
            .Setup(m => m.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi1" });

        harness.AssignmentService
            .Setup(m => m.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityReservation)null);

        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(
            new InboundVoiceEvent { ProviderName = "TestProvider", ProviderCallId = "call-1", FromAddress = "+15551112222", ToAddress = "+15553334444" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Routed);
        Assert.Equal("q1", result.QueueId);
        harness.IncomingCallDispatcher.Verify(d => d.DispatchAsync(It.IsAny<string>(), It.IsAny<TelephonyCall>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OfferNextAsync_WhenReservedAgentCannotBeLoaded_ReleasesReservation()
    {
        // Arrange
        var harness = new Harness();
        harness.AssignmentService
            .Setup(m => m.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1", AgentId = "a1", ActivityItemId = "act1", QueueId = "q1" });

        harness.AgentManager
            .Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentProfile)null);

        var service = harness.CreateService();

        // Act
        var agentUserId = await service.OfferNextAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(agentUserId);
        harness.ReservationService.Verify(s => s.RejectAsync("r1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class Harness
    {
        public Mock<IOmnichannelChannelEndpointManager> ChannelEndpointManager { get; } = new();

        public Mock<ISubjectFlowSettingsService> SubjectFlowSettingsService { get; } = new();

        public Mock<IOmnichannelActivityManager> ActivityManager { get; } = new();

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Mock<IActivityQueueManager> QueueManager { get; } = new();

        public Mock<IActivityQueueService> QueueService { get; } = new();

        public Mock<IActivityAssignmentService> AssignmentService { get; } = new();

        public Mock<IActivityReservationService> ReservationService { get; } = new();

        public Mock<IAgentProfileManager> AgentManager { get; } = new();

        public Mock<IInboundContactLookup> ContactLookup { get; } = new();

        public Mock<IIncomingCallDispatcher> IncomingCallDispatcher { get; } = new();

        public Mock<IContactCenterVoiceProviderResolver> VoiceProviderResolver { get; } = new();

        public Mock<IEntryPointResolver> EntryPointResolver { get; } = new();

        public void SetupNoContext()
        {
            ChannelEndpointManager
                .Setup(m => m.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<OmnichannelChannelEndpoint>((OmnichannelChannelEndpoint)null));

            SubjectFlowSettingsService
                .Setup(m => m.GetConfiguredFlowSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            ContactLookup
                .Setup(m => m.FindContactItemIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            ActivityManager
                .Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OmnichannelActivity { ItemId = "act1" });

            InteractionManager
                .Setup(m => m.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Interaction { ItemId = "int1" });
        }

        public VoiceContactCenterCallRouter CreateService()
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(_now);

            return new VoiceContactCenterCallRouter(
                ChannelEndpointManager.Object,
                SubjectFlowSettingsService.Object,
                ActivityManager.Object,
                ContentManager.Object,
                InteractionManager.Object,
                QueueManager.Object,
                QueueService.Object,
                AssignmentService.Object,
                ReservationService.Object,
                AgentManager.Object,
                ContactLookup.Object,
                IncomingCallDispatcher.Object,
                VoiceProviderResolver.Object,
                EntryPointResolver.Object,
                clock.Object);
        }
    }
}
