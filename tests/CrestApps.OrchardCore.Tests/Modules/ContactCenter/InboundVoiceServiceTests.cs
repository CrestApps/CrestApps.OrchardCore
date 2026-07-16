using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class InboundVoiceServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CanRouteOutbound_WhenDialCapabilityHasNoExecutableContract_ReturnsFalse()
    {
        // Arrange
        var harness = new Harness();
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider
            .SetupGet(value => value.Capabilities)
            .Returns(ContactCenterVoiceProviderCapabilities.DialerDial);
        harness.VoiceProviderResolver
            .Setup(value => value.Get("provider"))
            .Returns(provider.Object);
        var service = harness.CreateService();

        // Act
        var canRoute = service.CanRouteOutbound("provider");

        // Assert
        Assert.False(canRoute);
    }

    [Fact]
    public async Task RouteOutboundAsync_WhenDialCapabilityHasNoExecutableContract_FailsClosed()
    {
        // Arrange
        var harness = new Harness();
        var provider = new Mock<IContactCenterVoiceProvider>();
        provider
            .SetupGet(value => value.Capabilities)
            .Returns(ContactCenterVoiceProviderCapabilities.DialerDial);
        harness.VoiceProviderResolver
            .Setup(value => value.Get("provider"))
            .Returns(provider.Object);
        var service = harness.CreateService();

        // Act
        var result = await service.RouteOutboundAsync(
            new ContactCenterDialRequest
            {
                Destination = "+15551234567",
            },
            "provider",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("dialing_not_supported", result.ErrorCode);
    }

    [Fact]
    public async Task RouteOutboundAsync_WhenDialCapabilityHasExecutableContract_InvokesCallControlProvider()
    {
        // Arrange
        var harness = new Harness();
        var provider = new Mock<IContactCenterVoiceProvider>();
        var callControlProvider = provider.As<IContactCenterVoiceCallControlProvider>();
        provider
            .SetupGet(value => value.Capabilities)
            .Returns(ContactCenterVoiceProviderCapabilities.DialerDial);
        callControlProvider
            .Setup(value => value.DialAsync(It.IsAny<ContactCenterDialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult
            {
                Succeeded = true,
                ProviderCallId = "call-1",
            });
        harness.VoiceProviderResolver
            .Setup(value => value.Get("provider"))
            .Returns(provider.Object);
        var service = harness.CreateService();

        // Act
        var result = await service.RouteOutboundAsync(
            new ContactCenterDialRequest
            {
                Destination = "+15551234567",
            },
            "provider",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("call-1", result.ProviderCallId);
        callControlProvider.Verify(
            value => value.DialAsync(
                It.Is<ContactCenterDialRequest>(request => request.Destination == "+15551234567"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

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
        Assert.False(result.Queued);
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
        harness.OfferSynchronizationService.Verify(
            service => service.ReconcileEndedOfferAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleInboundAsync_WhenMultipleContactsMatch_AssignsFirstMatchToday()
    {
        // Arrange
        var harness = new Harness();
        harness.ChannelEndpointManager
            .Setup(manager => manager.GetByServiceAddressAsync(
                "Phone",
                "15553334444",
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OmnichannelChannelEndpoint>(
                new OmnichannelChannelEndpoint
                {
                    ItemId = "ep1",
                    Channel = "Phone",
                    Value = "+15553334444",
                }));
        harness.SubjectFlowSettingsService
            .Setup(service => service.GetConfiguredFlowSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new SubjectFlowSettings
                {
                    Channel = "Phone",
                    ChannelEndpointId = "ep1",
                    SubjectContentType = "CallSubject",
                },
            ]);
        harness.ContactLookup
            .Setup(service => service.FindContactItemIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["contact-a", "contact-b"]);
        harness.ContentManager
            .Setup(manager => manager.GetAsync("contact-a", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem
            {
                ContentItemId = "contact-a",
                ContentType = "Customer",
            });
        harness.ContentManager
            .Setup(manager => manager.NewAsync("CallSubject"))
            .ReturnsAsync(new ContentItem { ContentType = "CallSubject" });
        var activity = new OmnichannelActivity { ItemId = "act1" };
        harness.ActivityManager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        harness.QueueManager
            .Setup(manager => manager.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        harness.InteractionManager
            .Setup(manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "int1" });
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
        Assert.False(result.Routed);
        Assert.Equal("contact-a", activity.ContactContentItemId);
        Assert.Equal("Customer", activity.ContactContentType);
        harness.ActivityManager.Verify(
            manager => manager.CreateAsync(
                It.Is<OmnichannelActivity>(persistedActivity =>
                    ReferenceEquals(persistedActivity, activity) &&
                    persistedActivity.ContactContentItemId == "contact-a" &&
                    persistedActivity.ContactContentType == "Customer")),
            Times.Once);
        harness.ContentManager.Verify(
            manager => manager.GetAsync("contact-a", It.IsAny<VersionOptions>()),
            Times.Once);
        harness.ContentManager.Verify(
            manager => manager.GetAsync("contact-b", It.IsAny<VersionOptions>()),
            Times.Never);
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
        harness.OfferSynchronizationService.Verify(
            service => service.ReconcileEndedOfferAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        Assert.True(result.Queued);
        Assert.Equal("q1", result.QueueId);
        Assert.Equal("The call is waiting in the queue for the next eligible agent.", result.Reason);
        harness.OfferSynchronizationService.Verify(
            service => service.ReconcileEndedOfferAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleInboundAsync_WhenProviderCallIsAlreadyTracked_ReturnsExistingInteractionWithoutDuplicatingWork()
    {
        // Arrange
        var harness = new Harness();
        harness.InteractionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("TestProvider", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "interaction-1",
                ActivityItemId = "activity-1",
                ProviderInteractionId = "call-1",
                QueueId = "queue-1",
            });
        harness.QueueItemManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem
            {
                ActivityItemId = "activity-1",
                QueueId = "queue-1",
                Status = QueueItemStatus.Removed,
            });
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
        Assert.False(result.Routed);
        Assert.False(result.Queued);
        Assert.Equal("activity-1", result.ActivityItemId);
        Assert.Equal("interaction-1", result.InteractionId);
        harness.ActivityManager.Verify(
            manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.InteractionManager.Verify(
            manager => manager.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.QueueService.Verify(
            service => service.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleInboundAsync_WhenEndpointSpecificQueueExists_PrefersItOverGenericQueue()
    {
        // Arrange
        var harness = new Harness();
        harness.SetupNoContext();

        harness.ChannelEndpointManager
            .Setup(m => m.GetByServiceAddressAsync(It.IsAny<string>(), "+15553334444", It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<OmnichannelChannelEndpoint>(new OmnichannelChannelEndpoint { ItemId = "ep1", Channel = "Phone", Value = "+15553334444" }));

        harness.QueueManager
            .Setup(m => m.ListEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ActivityQueue { ItemId = "q-generic", Enabled = true },
                new ActivityQueue { ItemId = "q-endpoint", Enabled = true, InboundChannelEndpointId = "ep1" },
            ]);

        harness.QueueService
            .Setup(m => m.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi1" });

        harness.AssignmentService
            .Setup(m => m.AssignNextAsync("q-endpoint", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityReservation)null);

        var service = harness.CreateService();

        // Act
        var result = await service.HandleInboundAsync(
            new InboundVoiceEvent { ProviderName = "TestProvider", ProviderCallId = "call-1", FromAddress = "+15551112222", ToAddress = "+15553334444" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("q-endpoint", result.QueueId);
        harness.QueueService.Verify(m => m.EnqueueAsync("act1", "q-endpoint", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Once);
        harness.QueueService.Verify(m => m.EnqueueAsync("act1", "q-generic", It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Never);
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
        harness.OfferSynchronizationService.Verify(
            service => service.ReconcileEndedOfferAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OfferNextAsync_WhenPreviewDialActivityHasNoInteraction_PreservesReservation()
    {
        // Arrange
        var harness = new Harness();
        harness.AssignmentService
            .Setup(m => m.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "r1",
                AgentId = "a1",
                ActivityItemId = "act1",
                QueueId = "q1",
            });
        harness.AgentManager
            .Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                UserId = "u1",
            });
        harness.InteractionManager
            .Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);
        harness.ActivityManager
            .Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity
            {
                ItemId = "act1",
                Source = ActivitySources.PreviewDial,
            });

        var service = harness.CreateService();

        // Act
        var agentUserId = await service.OfferNextAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(agentUserId);
        harness.ReservationService.Verify(
            service => service.RejectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.OfferSynchronizationService.Verify(
            service => service.ReconcileEndedOfferAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OfferNextAsync_WhenAutomatedDialerActivityHasNoInteraction_ReleasesReservation()
    {
        // Arrange
        var harness = new Harness();
        harness.AssignmentService
            .Setup(m => m.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "r1",
                AgentId = "a1",
                ActivityItemId = "act1",
                QueueId = "q1",
            });
        harness.AgentManager
            .Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                UserId = "u1",
            });
        harness.InteractionManager
            .Setup(m => m.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);
        harness.ActivityManager
            .Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity
            {
                ItemId = "act1",
                Source = ActivitySources.PowerDial,
            });

        var service = harness.CreateService();

        // Act
        var agentUserId = await service.OfferNextAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(agentUserId);
        harness.ReservationService.Verify(
            service => service.RejectAsync("r1", It.IsAny<CancellationToken>()),
            Times.Once);
        harness.OfferSynchronizationService.Verify(
            service => service.ReconcileEndedOfferAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OfferNextAsync_WhenQueuedCallNoLongerExistsOnProvider_RemovesItAndOffersNextCall()
    {
        // Arrange
        var harness = new Harness();

        harness.AssignmentService
            .SetupSequence(m => m.AssignNextAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r-dead", AgentId = "a-dead", ActivityItemId = "act-dead", QueueId = "q1" })
            .ReturnsAsync(new ActivityReservation { ItemId = "r-live", AgentId = "a-live", ActivityItemId = "act-live", QueueId = "q1" });

        harness.AgentManager
            .Setup(m => m.FindByIdAsync("a-dead", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a-dead", UserId = "user-dead" });

        harness.AgentManager
            .Setup(m => m.FindByIdAsync("a-live", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a-live", UserId = "user-live" });

        harness.InteractionManager
            .Setup(m => m.FindByActivityIdAsync("act-dead", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "int-dead",
                ActivityItemId = "act-dead",
                ProviderInteractionId = "call-dead",
                Status = InteractionStatus.Ended,
            });

        harness.InteractionManager
            .Setup(m => m.FindByActivityIdAsync("act-live", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction
            {
                ItemId = "int-live",
                ActivityItemId = "act-live",
                ProviderInteractionId = "call-live",
                Status = InteractionStatus.Ringing,
            });

        var service = harness.CreateService();

        // Act
        var agentUserId = await service.OfferNextAsync("q1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("user-live", agentUserId);
        harness.OfferSynchronizationService.Verify(s => s.ReconcileEndedOfferAsync("int-dead", It.IsAny<CancellationToken>()), Times.Once);
        harness.OfferSynchronizationService.Verify(
            s => s.ReconcileEndedOfferAsync("int-live", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class Harness
    {
        public Mock<IOmnichannelChannelEndpointManager> ChannelEndpointManager { get; } = new();

        public Mock<ISubjectFlowSettingsService> SubjectFlowSettingsService { get; } = new();

        public Mock<IOmnichannelActivityManager> ActivityManager { get; } = new();

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Mock<IActivityQueueManager> QueueManager { get; } = new();

        public Mock<IQueueItemManager> QueueItemManager { get; } = new();

        public Mock<IActivityQueueService> QueueService { get; } = new();

        public Mock<IActivityAssignmentService> AssignmentService { get; } = new();

        public Mock<IActivityReservationService> ReservationService { get; } = new();

        public Mock<IAgentProfileManager> AgentManager { get; } = new();

        public Mock<IInboundContactLookup> ContactLookup { get; } = new();

        public Mock<IContactCenterVoiceProviderResolver> VoiceProviderResolver { get; } = new();

        public Mock<IEntryPointResolver> EntryPointResolver { get; } = new();

        public Mock<IProviderVoiceOfferSynchronizationService> OfferSynchronizationService { get; } = new();

        public Mock<IDistributedLock> DistributedLock { get; } = new();

        public Harness()
        {
            DistributedLock
                .Setup(l => l.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync((null, true));
        }

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
                QueueItemManager.Object,
                QueueService.Object,
                AssignmentService.Object,
                ReservationService.Object,
                AgentManager.Object,
                ContactLookup.Object,
                VoiceProviderResolver.Object,
                [EntryPointResolver.Object],
                OfferSynchronizationService.Object,
                DistributedLock.Object,
                new TestContactCenterScopeExecutor(new ServiceCollection().BuildServiceProvider()),
                new TestContactCenterFeatureWorkManager(),
                clock.Object);
        }
    }
}
