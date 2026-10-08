using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What the live dashboard's state poll puts on a supervisor's agent board. Confirmed live: an agent signed in only to a
/// campaign, whose work runs under a queue that is never stored, is on the board of a supervisor who oversees that
/// campaign, and is offered under its campaign filter.
/// </summary>
public sealed class SupervisorDashboardStateTests
{
    private const string SupervisorId = "supervisor-1";

    [Fact]
    public async Task StatePoll_AnAgentSignedInOnlyToACampaignTheSupervisorOversees_IsOnTheBoardUnderThatCampaign()
    {
        // Arrange
        var probe = new DashboardProbe();
        probe.AddAgent(new AgentProfile
        {
            ItemId = "agent-renewals",
            UserId = "user-renewals",
            DisplayName = "Rene Renewals",
            PresenceStatus = AgentPresenceStatus.Available,
            CampaignIds = ["renewals"],
        });
        probe.Authorize(ContactCenterConstants.CampaignQueue.CreateId("renewals"));

        // Act
        var model = await probe.PollAsync();

        // Assert
        var row = Assert.Single(model.Agents);
        Assert.Equal("agent-renewals", row.AgentId);
        Assert.Equal(["renewals"], row.CampaignIds);
        Assert.Empty(row.QueueIds);
        Assert.Contains(model.Campaigns, campaign => campaign.Id == "renewals");
        Assert.Equal(1, model.AvailableAgents);
    }

    // A campaign the supervisor does not oversee keeps its agents off their board, as another team's queue does.
    [Fact]
    public async Task StatePoll_AnAgentSignedInOnlyToAnotherTeamsCampaign_IsNotOnTheBoard()
    {
        // Arrange
        var probe = new DashboardProbe();
        probe.AddAgent(new AgentProfile
        {
            ItemId = "agent-winback",
            UserId = "user-winback",
            PresenceStatus = AgentPresenceStatus.Available,
            CampaignIds = ["winback"],
        });
        probe.Authorize(ContactCenterConstants.CampaignQueue.CreateId("renewals"));

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Empty(model.Agents);
        Assert.DoesNotContain(model.Campaigns, campaign => campaign.Id == "winback");
    }

    // An agent on a queue and a campaign is filed under both: the stored queue among the queues, and the campaign's
    // unstored queue only as the campaign, so the queue filter never offers it.
    [Fact]
    public async Task StatePoll_AnAgentOnAQueueAndACampaign_ListsTheQueueAndTheCampaignApart()
    {
        // Arrange
        var probe = new DashboardProbe();
        probe.AddQueue("support");
        probe.AddAgent(new AgentProfile
        {
            ItemId = "agent-both",
            UserId = "user-both",
            PresenceStatus = AgentPresenceStatus.Busy,
            QueueIds = ["support", ContactCenterConstants.CampaignQueue.CreateId("renewals")],
            CampaignIds = ["renewals"],
        });
        probe.Authorize("support", ContactCenterConstants.CampaignQueue.CreateId("renewals"));

        // Act
        var model = await probe.PollAsync();

        // Assert
        var row = Assert.Single(model.Agents);
        Assert.Equal(["support"], row.QueueIds);
        Assert.Equal(["renewals"], row.CampaignIds);
        Assert.Equal("support", Assert.Single(model.Queues).Id);
    }

    private sealed class DashboardProbe
    {
        private readonly List<AgentProfile> _agents = [];
        private readonly List<ActivityQueue> _queues = [];
        private readonly HashSet<string> _authorizedQueueIds = new(StringComparer.OrdinalIgnoreCase);

        public void AddAgent(AgentProfile agent)
            => _agents.Add(agent);

        public void AddQueue(string queueId)
            => _queues.Add(new ActivityQueue { ItemId = queueId, Name = queueId, Enabled = true });

        public void Authorize(params string[] queueIds)
            => _authorizedQueueIds.UnionWith(queueIds);

        public async Task<SupervisorDashboardStateViewModel> PollAsync()
        {
            var agentManager = new Mock<IAgentProfileManager>();
            agentManager
                .Setup(manager => manager.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<QueryContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PageResult<AgentProfile> { Count = _agents.Count, Entries = _agents.ToArray() });

            var queueManager = new Mock<IActivityQueueManager>();
            queueManager
                .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_queues);

            var queueItemManager = new Mock<IQueueItemManager>();
            queueItemManager
                .Setup(manager => manager.CountWaitingByQueueIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, int>(StringComparer.Ordinal));

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager
                .Setup(manager => manager.GetActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            interactionManager
                .Setup(manager => manager.CountActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, int>(StringComparer.Ordinal));

            var queueAuthorization = new Mock<ISupervisorQueueAuthorizationService>();
            queueAuthorization
                .Setup(service => service.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), SupervisorId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ClaimsPrincipal _, string _, string queueId, CancellationToken _) => _authorizedQueueIds.Contains(queueId));

            var phoneCalls = new Mock<IContactCenterPhoneCallSupervisionService>();
            phoneCalls
                .Setup(service => service.FindCallsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, AgentPhoneCall>(StringComparer.Ordinal));

            var result = await SupervisorDashboardEndpoints.HandleStateAsync(
                new AllowingAuthorizationService(),
                queueManager.Object,
                queueItemManager.Object,
                agentManager.Object,
                interactionManager.Object,
                queueAuthorization.Object,
                [],
                // The display names are read from the users' records; none are stored here, so each agent keeps the name
                // on their profile.
                new Mock<ISession> { DefaultValue = DefaultValue.Mock }.Object,
                Mock.Of<IDisplayNameProvider>(),
                new StubClock(),
                new SupervisorDashboardInterventionDescriber(
                    Mock.Of<ICallSessionManager>(),
                    Mock.Of<IContactCenterVoiceProviderResolver>(),
                    [],
                    phoneCalls.Object,
                    new PassThroughStringLocalizer<SupervisorDashboardInterventionDescriber>()),
                new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, SupervisorId)], "Test")),
                    RequestServices = new ServiceCollection().BuildServiceProvider(),
                });

            var value = result.GetType().GetProperty("Value")?.GetValue(result);

            return Assert.IsType<SupervisorDashboardStateViewModel>(value);
        }
    }

    private sealed class AllowingAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }
}
