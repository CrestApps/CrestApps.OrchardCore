using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using CcAgentAvailabilityReader = CrestApps.OrchardCore.ContactCenter.Reports.Services.AgentAvailabilityReader;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AI;

/// <summary>
/// Builds the request a Contact Center AI tool runs in: a signed-in supervisor, the permission check, the oversight check
/// and the clock, with each test adding the services its tool reads. Every service is a double, so no test reads or writes
/// production data.
/// </summary>
internal sealed class ContactCenterToolHost
{
    public static readonly DateTime Now = new(2026, 11, 6, 18, 0, 0, DateTimeKind.Utc);

    public const string SupervisorId = "supervisor-1";

    private readonly List<string> _unoverseen = [];

    public ContactCenterToolHost(bool authenticated = true, bool permitted = true)
    {
        var principal = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, SupervisorId)], "Test"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        Permission = new FakeAuthorization(permitted);
        Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
        Services.AddSingleton<IAuthorizationService>(Permission);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(Now);
        Services.AddSingleton(clock.Object);

        var oversight = new Mock<ISupervisorQueueAuthorizationService>();
        oversight
            .Setup(service => service.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal _, string _, string queueId, CancellationToken _) => !_unoverseen.Contains(queueId));
        Services.AddSingleton(oversight.Object);
    }

    public ServiceCollection Services { get; } = [];

    public FakeAuthorization Permission { get; }

    /// <summary>
    /// Says the supervisor does not oversee a queue or campaign queue.
    /// </summary>
    public ContactCenterToolHost DoesNotOversee(string queueId)
    {
        _unoverseen.Add(queueId);

        return this;
    }

    public ContactCenterToolHost WithQueues(params ActivityQueue[] queues)
    {
        var manager = new Mock<IActivityQueueManager>();
        manager.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(queues.ToList());
        Services.AddSingleton(manager.Object);

        return this;
    }

    public ContactCenterToolHost WithCampaigns(params (string Id, string Name)[] campaigns)
    {
        var manager = new Mock<ICatalogManager<OmnichannelCampaign>>();
        manager
            .Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(campaigns.Select(campaign => new OmnichannelCampaign { ItemId = campaign.Id, DisplayText = campaign.Name }).ToList());
        Services.AddSingleton(manager.Object);

        return this;
    }

    public ContactCenterToolHost WithAgents(params AgentProfile[] agents)
    {
        var manager = new Mock<IAgentProfileManager>();
        manager.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agents.ToList());
        manager
            .Setup(value => value.GetAvailableForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string queueId, CancellationToken _) => agents
                .Where(agent => agent.PresenceStatus == AgentPresenceStatus.Available && AgentEntitlementUtilities.HasQueueEntitlement(agent, queueId))
                .ToArray());
        Services.AddSingleton(manager.Object);

        return this;
    }

    public ContactCenterToolHost WithDecisionLog(params DialerDecisionIndex[] entries)
    {
        var log = new Mock<IDialerDecisionLog>();
        log
            .Setup(value => value.GetEntriesAsync(It.IsAny<DialerDecisionLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DialerDecisionLogQuery query, CancellationToken _) => (IReadOnlyList<DialerDecisionIndex>)entries
                .Where(entry => (query.AgentId is null || entry.AgentId == query.AgentId) &&
                    (query.ActivityItemId is null || entry.ActivityItemId == query.ActivityItemId) &&
                    (query.CampaignId is null || entry.CampaignId == query.CampaignId) &&
                    (query.Reason is null || entry.Reason == query.Reason) &&
                    (query.EventType is null || entry.EventType == query.EventType) &&
                    (!query.FromUtc.HasValue || entry.LastSeenUtc >= query.FromUtc) &&
                    (!query.ToUtc.HasValue || entry.OccurredUtc <= query.ToUtc))
                .ToArray());
        log
            .Setup(value => value.GetLastRoutingDecisionTimesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, DateTime>());
        Services.AddSingleton(log.Object);

        return this;
    }

    /// <summary>
    /// Registers the agent availability reader with real routing and availability code over doubles, as the report's own
    /// tests do, so the reasons are the reasons routing acts on.
    /// </summary>
    public ContactCenterToolHost WithAvailability(
        IReadOnlyList<AgentProfile> agents,
        IReadOnlyList<AgentSession> sessions,
        IReadOnlyDictionary<string, int> activeCounts,
        IReadOnlyList<Interaction> activeInteractions = null,
        IReadOnlyList<Interaction> pendingWrapUps = null,
        IReadOnlyList<InteractionEvent> offerEvents = null,
        IReadOnlyList<ActivityQueue> queues = null,
        IReadOnlyList<ActivityReservation> reservations = null,
        IReadOnlyDictionary<string, int> waiting = null)
    {
        var clock = Services.BuildServiceProvider().GetRequiredService<IClock>();
        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager
            .Setup(manager => manager.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> userIds, CancellationToken _) => sessions.Where(session => userIds.Contains(session.UserId)).ToArray());

        var interactions = new Mock<IInteractionManager>();
        interactions
            .Setup(manager => manager.CountActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>(activeCounts, StringComparer.Ordinal));
        interactions
            .Setup(manager => manager.GetActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((activeInteractions ?? []).ToArray());
        interactions
            .Setup(manager => manager.GetPendingWrapUpsByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((pendingWrapUps ?? []).ToArray());

        var events = new Mock<IInteractionEventStore>();
        events
            .Setup(store => store.GetByAggregateWindowAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns((string aggregateType, IEnumerable<string> _, IEnumerable<string> _, DateTime _, DateTime _, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<InteractionEvent>>(aggregateType == nameof(ActivityReservation) ? (offerEvents ?? []).ToArray() : []));
        events
            .Setup(store => store.GetLatestBeforeAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((queues ?? []).ToList());

        var queueItems = new Mock<IQueueItemManager>();
        queueItems
            .Setup(manager => manager.CountWaitingByQueueIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>(waiting ?? new Dictionary<string, int>(), StringComparer.Ordinal));

        var hours = new Mock<IBusinessHoursService>();
        hours.Setup(service => service.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var campaigns = new Mock<ICatalogManager<OmnichannelCampaign>>();
        campaigns.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<OmnichannelCampaign>());

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agents.ToList());
        agentManager
            .Setup(manager => manager.GetAvailableForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string queueId, CancellationToken _) => agents
                .Where(agent => agent.PresenceStatus == AgentPresenceStatus.Available && AgentEntitlementUtilities.HasQueueEntitlement(agent, queueId))
                .ToArray());

        var options = Options.Create(new AgentAvailabilityOptions());
        var reader = new CcAgentAvailabilityReader(
            sessionManager.Object,
            interactions.Object,
            events.Object,
            queueManager.Object,
            queueItems.Object,
            hours.Object,
            new AgentAvailabilityService(agentManager.Object, sessionManager.Object, interactions.Object, options, clock),
            new ActivityRoutingService([new RequiredSkillsRoutingStrategy(clock), new CapacityRoutingStrategy(), new LongestIdleRoutingStrategy()]),
            campaigns.Object,
            [],
            options,
            clock,
            Mock.Of<ILocalClock>());

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(manager => manager.GetActiveByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((reservations ?? []).ToArray());

        Services.AddSingleton(reader);
        Services.AddSingleton(interactions.Object);
        Services.AddSingleton(events.Object);
        Services.AddSingleton(reservationManager.Object);
        Services.AddSingleton(agentManager.Object);
        Services.AddSingleton(queueManager.Object);
        Services.AddSingleton(campaigns.Object);

        return this;
    }

    public AIFunctionArguments Arguments(params (string Name, object Value)[] values)
        => new(values.ToDictionary(value => value.Name, value => value.Value))
        {
            Services = Services.BuildServiceProvider(),
        };

    /// <summary>
    /// Runs a tool and reads what it returned as JSON.
    /// </summary>
    public async Task<JsonElement> RunAsync(AIFunction tool, params (string Name, object Value)[] values)
    {
        var result = await tool.InvokeAsync(Arguments(values), TestContext.Current.CancellationToken);
        var text = Assert.IsType<string>(result);

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static AgentProfile Agent(string id, string userName, AgentPresenceStatus presence = AgentPresenceStatus.Available, params string[] queueIds)
        => new()
        {
            ItemId = id,
            UserId = "user-" + id,
            UserName = userName,
            DisplayName = userName,
            PresenceStatus = presence,
            PresenceChangedUtc = Now.AddMinutes(-30),
            IdleSinceUtc = presence == AgentPresenceStatus.Available ? Now.AddMinutes(-30) : null,
            QueueIds = [.. queueIds],
            AllowedQueueIds = [.. queueIds],
        };

    public static AgentSession LiveSession(AgentProfile agent, params string[] queueIds)
        => new()
        {
            UserId = agent.UserId,
            IsOnline = true,
            ConnectionIds = ["c-" + agent.ItemId],
            LastHeartbeatUtc = Now.AddSeconds(-5),
            QueueIds = [.. queueIds],
        };

    public static string[] FindingCodes(JsonElement result)
        => [.. result.GetProperty("findings").EnumerateArray().Select(finding => finding.GetProperty("code").GetString())];

    public sealed class FakeAuthorization : IAuthorizationService
    {
        private readonly bool _permitted;

        public FakeAuthorization(bool permitted)
        {
            _permitted = permitted;
        }

        public int Checks { get; private set; }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            Checks++;

            return Task.FromResult(_permitted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
        {
            Checks++;

            return Task.FromResult(_permitted ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }
    }
}
