using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterTransferDirectoryServiceTests
{
    [Fact]
    public async Task GetAsync_ListsEveryOtherAgentWithTheirPresence_AndNeverTheAgentAsking()
    {
        var directory = await CreateService().GetAsync("user-a", Principal(), "Telnyx", TestContext.Current.CancellationToken);

        Assert.DoesNotContain(directory.Agents, agent => agent.Id == "agent-a");
        Assert.Collection(
            directory.Agents,
            agent =>
            {
                Assert.Equal("agent-b", agent.Id);
                Assert.Equal("Bea Baker", agent.Name);
                Assert.Equal("201", agent.Extension);
                Assert.Equal(AgentPresenceStatus.Available, agent.Presence);
                Assert.True(agent.Available);
            },
            agent =>
            {
                Assert.Equal("agent-c", agent.Id);
                Assert.Equal(AgentPresenceStatus.Busy, agent.Presence);
                Assert.False(agent.Available);
            },
            agent =>
            {
                // Available, but already at capacity: offering them a call would ring an agent who cannot take it.
                Assert.Equal("agent-d", agent.Id);
                Assert.Equal(AgentPresenceStatus.Available, agent.Presence);
                Assert.False(agent.Available);
            });
    }

    [Fact]
    public async Task GetAsync_ListsEnabledQueuesWithHowManyCallersAreWaiting()
    {
        var directory = await CreateService().GetAsync("user-a", Principal(), "Telnyx", TestContext.Current.CancellationToken);

        var queue = Assert.Single(directory.Queues);
        Assert.Equal("queue-sales", queue.Id);
        Assert.Equal("Sales", queue.Name);
        Assert.Equal(3, queue.Waiting);
    }

    [Fact]
    public async Task GetAsync_OffersExternalDestinations_OnlyToAnAgentAllowedToTransferExternally()
    {
        var allowed = await CreateService(canTransferExternally: true, allowUnlistedNumbers: true)
            .GetAsync("user-a", Principal(), "Telnyx", TestContext.Current.CancellationToken);
        var refused = await CreateService(canTransferExternally: false, allowUnlistedNumbers: true)
            .GetAsync("user-a", Principal(), "Telnyx", TestContext.Current.CancellationToken);

        var destination = Assert.Single(allowed.ExternalDestinations);
        Assert.Equal("dest-1", destination.Id);
        Assert.Equal("Billing partner", destination.Name);
        Assert.Equal("+15559990000", destination.Number);
        Assert.True(allowed.CanTransferExternally);
        Assert.True(allowed.AllowExternalNumbers);

        Assert.Empty(refused.ExternalDestinations);
        Assert.False(refused.CanTransferExternally);
        Assert.False(refused.AllowExternalNumbers);
    }

    [Fact]
    public async Task GetAsync_AllowsTypedNumbers_OnlyWhenTheTenantTurnedThemOn()
    {
        var directory = await CreateService(canTransferExternally: true, allowUnlistedNumbers: false)
            .GetAsync("user-a", Principal(), "Telnyx", TestContext.Current.CancellationToken);

        Assert.False(directory.AllowExternalNumbers);
    }

    [Fact]
    public async Task GetAsync_OffersWarmTransfer_OnlyWhenTheCallsProviderCanConsult()
    {
        var consulting = await CreateService(providerConsults: true).GetAsync("user-a", Principal(), "Telnyx", TestContext.Current.CancellationToken);
        var blindOnly = await CreateService(providerConsults: false).GetAsync("user-a", Principal(), "Telnyx", TestContext.Current.CancellationToken);

        Assert.True(consulting.SupportsConsult);
        Assert.False(blindOnly.SupportsConsult);
    }

    [Fact]
    public async Task GetAsync_DoesNotOfferWarmTransfer_WhenTheCallsTelephonyProviderCannotHoldForAConsult()
    {
        // A provider whose Contact Center adapter can place a consult but whose telephony side never reports the
        // destination answering would leave the agent watching "Calling..." with no way to complete.
        var directory = await CreateService(providerConsults: true, telephonyConsults: false)
            .GetAsync("user-a", Principal(), "Telnyx", TestContext.Current.CancellationToken);

        Assert.False(directory.SupportsConsult);
    }

    private static ClaimsPrincipal Principal()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-a")], "Test"));

    private static ContactCenterTransferDirectoryService CreateService(
        bool canTransferExternally = true,
        bool allowUnlistedNumbers = false,
        bool providerConsults = true,
        bool telephonyConsults = true)
    {
        var agents = new[]
        {
            new AgentProfile { ItemId = "agent-a", UserId = "user-a", DisplayName = "Ada Adams", PresenceStatus = AgentPresenceStatus.Busy, MaxConcurrentInteractions = 1 },
            new AgentProfile { ItemId = "agent-d", UserId = "user-d", DisplayName = "Dee Dunn", PresenceStatus = AgentPresenceStatus.Available, MaxConcurrentInteractions = 1 },
            new AgentProfile { ItemId = "agent-b", UserId = "user-b", DisplayName = "Bea Baker", PresenceStatus = AgentPresenceStatus.Available, MaxConcurrentInteractions = 1 },
            new AgentProfile { ItemId = "agent-c", UserId = "user-c", UserName = "cal", DisplayName = "Cal Cole", PresenceStatus = AgentPresenceStatus.Busy, MaxConcurrentInteractions = 1 },
        };

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agents);

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.CountActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["agent-d"] = 1, ["agent-c"] = 1 });

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActivityQueue { ItemId = "queue-sales", Name = "Sales", Enabled = true }]);

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.CountWaitingByQueueIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["queue-sales"] = 3 });

        var extensionManager = new Mock<ITelephonyExtensionManager>();
        extensionManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TelephonyExtension { Number = "201", UserId = "user-b" }]);

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(canTransferExternally ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var settings = new ContactCenterExternalTransferSettings
        {
            AllowUnlistedNumbers = allowUnlistedNumbers,
            Destinations =
            [
                new ContactCenterExternalDestination { Id = "dest-1", DisplayName = "Billing partner", E164Address = "+15559990000", Enabled = true },
                new ContactCenterExternalDestination { Id = "dest-2", DisplayName = "Retired line", E164Address = "+15559990001", Enabled = false },
            ],
        };

        var provider = new Mock<IContactCenterVoiceProvider>();

        if (providerConsults)
        {
            provider.As<IContactCenterVoiceAttendedTransferProvider>();
        }

        var providerResolver = new Mock<IContactCenterVoiceProviderResolver>();
        providerResolver.Setup(resolver => resolver.Get(It.IsAny<string>())).Returns(provider.Object);

        var telephonyProvider = new Mock<ITelephonyProvider>();
        telephonyProvider.SetupGet(value => value.Capabilities).Returns(telephonyConsults
            ? TelephonyCapabilities.Transfer | TelephonyCapabilities.AttendedTransfer
            : TelephonyCapabilities.Transfer);
        var telephonyResolver = new Mock<ITelephonyProviderResolver>();
        telephonyResolver.Setup(resolver => resolver.GetAsync(It.IsAny<string>())).ReturnsAsync(telephonyProvider.Object);

        return new ContactCenterTransferDirectoryService(
            agentManager.Object,
            interactionManager.Object,
            queueManager.Object,
            queueItemManager.Object,
            [extensionManager.Object],
            authorizationService.Object,
            SiteServiceFactory.Create(settings),
            providerResolver.Object,
            telephonyResolver.Object);
    }
}
