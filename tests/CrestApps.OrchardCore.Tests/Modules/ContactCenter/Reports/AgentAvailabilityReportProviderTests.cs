using System.Globalization;
using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// Seeds the incident the agent availability report exists for, and every other stale or ineligible case, and checks that
/// the report says why each agent is not getting calls.
/// </summary>
/// <remarks>
/// The incident: one agent showed "On a call" with nothing on the line because a soft-phone call record the provider
/// could not resolve was still open; two agents showed about 21 hours Available in the time summary while the live
/// dashboard did not list them; and nobody could tell whether any of them was signed in to the campaign that had work.
/// The routing service and the availability gate are the real ones, so the reasons asserted are the reasons routing acts on.
/// </remarks>
public sealed class AgentAvailabilityReportProviderTests
{
    private const string CampaignId = "spring";
    private const string SupportQueueId = "q-support";
    private const string OtherQueueId = "q-other";
    private const string SupervisorUserId = "supervisor-user";

    private static readonly DateTime _now = new(2026, 7, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string _campaignQueueId = ContactCenterConstants.CampaignQueue.CreateId(CampaignId);

    [Fact]
    public async Task RunAsync_WhenAgentIsOnACallHeldByAnUnresolvedSoftPhoneRecord_FlagsTheRecordAndExplainsTheCampaign()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var agent = AgentRow(document, "ghost");
        Assert.Equal("Busy", agent.Cells[1]);
        Assert.Contains("Unresolved soft-phone call record.", agent.Cells[12], StringComparison.Ordinal);
        Assert.Contains("A phone call is still open although the provider cannot resolve it.", agent.Cells[12], StringComparison.Ordinal);
        Assert.Equal("Yes", agent.Cells[11]);

        var campaign = QueueRow(document, "ghost", "Spring promo");
        Assert.Equal("No", campaign.Cells[6]);
        Assert.Contains("Not Available (presence is Busy).", campaign.Cells[7], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenAvailableAgentHasNoSessionAndNoSignOut_ShowsAgentTheDashboardOmitsAndWhy()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var agent = AgentRow(document, "absent1");
        Assert.Equal("Available", agent.Cells[1]);
        Assert.Equal("2026-07-09 15:00:00.000", agent.Cells[2]);
        Assert.Equal("21:00:00", agent.Cells[3]);
        Assert.Equal("12:00:00", agent.Cells[7]);
        Assert.Equal("Nothing", agent.Cells[8]);
        Assert.Equal("No session", agent.Cells[9]);
        Assert.Equal("No: not signed in to any queue or campaign", agent.Cells[11]);
        Assert.Contains("Available with no live session (no session at all).", agent.Cells[12], StringComparison.Ordinal);
        Assert.Contains("the time summary counts this agent as Available up to now, but the agent has no live session", agent.Cells[13], StringComparison.Ordinal);

        var campaign = QueueRow(document, "absent1", "Spring promo");
        Assert.Equal("Yes", campaign.Cells[3]);
        Assert.Equal("No", campaign.Cells[4]);
        Assert.Equal("3", campaign.Cells[5]);
        Assert.Equal("No", campaign.Cells[6]);
        Assert.Contains("Not signed in to this campaign.", campaign.Cells[7], StringComparison.Ordinal);
        Assert.Contains("No live session.", campaign.Cells[7], StringComparison.Ordinal);
        Assert.DoesNotContain("Not entitled", campaign.Cells[7], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenAvailableAgentIsSignedInOnlyOutsideTheSupervisorsScope_ExplainsTheDashboardAndTheStaleHeartbeat()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var agent = AgentRow(document, "absent2");
        Assert.Equal("Other", agent.Cells[8]);
        Assert.Equal("No: offline, disconnected or no recent heartbeat", agent.Cells[9]);
        Assert.Equal("0:30:00", agent.Cells[10]);
        Assert.Equal("No: not signed in to a queue or campaign you oversee", agent.Cells[11]);
        Assert.Contains("Available with no live session (last heartbeat 2026-07-10 11:30:00.000 UTC).", agent.Cells[12], StringComparison.Ordinal);

        var other = QueueRow(document, "absent2", "Other");
        Assert.Equal("Yes", other.Cells[4]);
        Assert.Equal("No live session.", other.Cells[7]);

        var campaign = QueueRow(document, "absent2", "Spring promo");
        Assert.Contains("Not signed in to this campaign.", campaign.Cells[7], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenAgentPassesTheGate_UsesRoutingsOwnDecisionForTheNextWaitingItem()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var ready = QueueRow(document, "ready", "Spring promo");
        Assert.Equal("Yes", ready.Cells[6]);
        Assert.Equal("None: next in line for the waiting work.", ready.Cells[7]);
        Assert.Contains("Longest-idle rank", ready.Cells[8], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenAgentTransferredTheWaitingCallAway_ReportsTheRoutersExclusion()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var transferrer = QueueRow(document, "transferrer", "Spring promo");
        Assert.Equal("No", transferrer.Cells[6]);
        Assert.StartsWith("Routing passes this agent over for the next waiting item", transferrer.Cells[7], StringComparison.Ordinal);
        Assert.Contains("Transferred this call away, so it goes to an agent who has not had it first.", transferrer.Cells[8], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenAgentIsAtCapacity_SaysSo()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var support = QueueRow(document, "full", "Support");
        Assert.Equal("No", support.Cells[6]);
        Assert.Equal("At capacity (1/1 active interactions).", support.Cells[7]);
    }

    [Fact]
    public async Task RunAsync_WhenAgentIsReservedWithNothingBehindIt_FlagsTheStateAndTheHeldReservation()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var stuck = AgentRow(document, "stuck");
        Assert.Contains("Reserved with no live interaction.", stuck.Cells[12], StringComparison.Ordinal);
        Assert.Contains("Held reservation that no offer or call backs.", stuck.Cells[12], StringComparison.Ordinal);
        Assert.DoesNotContain("Holds reservation res-1.", stuck.Cells[12], StringComparison.Ordinal);

        var ringing = AgentRow(document, "ringing");
        Assert.Equal("Holds reservation res-2.", ringing.Cells[12]);
    }

    [Fact]
    public async Task RunAsync_WhenProfileAndRecordedHistoryDisagree_AddsANote()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var mismatch = AgentRow(document, "mismatch");
        Assert.Contains("The profile says Offline, but the last recorded change is to Available at 2026-07-10 10:00:00.000 UTC.", mismatch.Cells[13], StringComparison.Ordinal);
        Assert.Contains("No sign-out is recorded after the change to Available", mismatch.Cells[13], StringComparison.Ordinal);

        var unrecorded = AgentRow(document, "unrecorded");
        Assert.Contains("No state change is recorded for this agent", unrecorded.Cells[13], StringComparison.Ordinal);

        var ready = AgentRow(document, "ready");
        Assert.Equal("—", ready.Cells[13]);
    }

    [Fact]
    public async Task RunAsync_WhenAgentHasCalledToday_CountsCallsAndTheLongestWaitBetweenThem()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var ready = AgentRow(document, "ready");
        Assert.Equal("2", ready.Cells[6]);
        Assert.Equal("2026-07-10 11:55:00.000", ready.Cells[5]);
        Assert.Equal("2:00:00", ready.Cells[7]);
        Assert.Equal("Campaign: Spring promo", ready.Cells[8]);
        Assert.Equal("Yes", ready.Cells[9]);
    }

    [Fact]
    public async Task RunAsync_WhenSupervisorOverseesSomeQueues_ListsEntitledAgentsTheDashboardOmitsButNotOthers()
    {
        // Act
        var document = await RunIncidentAsync();

        // Assert
        var names = AgentNames(document);
        Assert.Contains("absent1", names);
        Assert.Contains("absent2", names);
        Assert.DoesNotContain("outsider", names);
        Assert.Contains("Not on your live dashboard", document.Sections[0].Metrics.Select(metric => metric.Label));
    }

    [Fact]
    public async Task RunAsync_WhenAnAgentIsChosen_ShowsThemEvenOutsideTheSupervisorsScope()
    {
        // Act
        var document = await RunIncidentAsync(agentId: "outsider");

        // Assert
        Assert.Equal(new[] { "outsider" }, AgentNames(document));
        Assert.Equal("No: not signed in to a queue or campaign you oversee", AgentRow(document, "outsider").Cells[11]);
    }

    [Fact]
    public async Task RunAsync_WhenStaleWorkDetectionIsNotEnabled_SaysItWasNotChecked()
    {
        // Act
        var document = await RunIncidentAsync(withStaleWorkDetection: false);

        // Assert
        Assert.Contains(
            "Stale work not checked",
            AgentRow(document, "ghost").Cells[12],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenAgentHasNoQueueOrCampaignAtAll_SaysSo()
    {
        // Act
        var document = await RunIncidentAsync(agentId: "loner");

        // Assert
        var row = Assert.Single(document.Sections[2].Rows);
        Assert.Equal("Not entitled to or signed in to any queue or campaign.", row.Cells[7]);
    }

    [Fact]
    public void StartOfDayUtc_WhenTenantIsBehindUtc_ReturnsTheLocalMidnight()
    {
        // Arrange
        var zone = ReportTimeZone.FromId("America/Los_Angeles");

        // Act
        var startOfDay = zone.StartOfDayUtc(new DateTime(2026, 7, 10, 3, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(new DateTime(2026, 7, 9, 7, 0, 0, DateTimeKind.Utc), startOfDay);
    }

    private static async Task<ReportDocument> RunIncidentAsync(string agentId = null, bool withStaleWorkDetection = true)
    {
        var agents = new List<AgentProfile>
        {
            CampaignAgent("ghost", AgentPresenceStatus.Busy, presenceChangedUtc: _now.AddHours(-3)),
            new()
            {
                ItemId = "absent1",
                UserId = "user-absent1",
                UserName = "absent1",
                PresenceStatus = AgentPresenceStatus.Available,
                PresenceChangedUtc = _now.AddHours(-21),
                IdleSinceUtc = _now.AddHours(-21),
                AllowedCampaignIds = [CampaignId],
            },
            new()
            {
                ItemId = "absent2",
                UserId = "user-absent2",
                UserName = "absent2",
                PresenceStatus = AgentPresenceStatus.Available,
                PresenceChangedUtc = _now.AddHours(-21),
                IdleSinceUtc = _now.AddHours(-21),
                QueueIds = [OtherQueueId],
                AllowedQueueIds = [OtherQueueId],
                AllowedCampaignIds = [CampaignId],
            },
            CampaignAgent("ready", AgentPresenceStatus.Available, presenceChangedUtc: _now.AddMinutes(-5), idleSinceUtc: _now.AddMinutes(-5)),
            CampaignAgent("transferrer", AgentPresenceStatus.Available, presenceChangedUtc: _now.AddHours(-1), idleSinceUtc: _now.AddHours(-1)),
            SupportAgent("full", AgentPresenceStatus.Available),
            SupportAgent("stuck", AgentPresenceStatus.Reserved, activeReservationId: "res-1"),
            SupportAgent("ringing", AgentPresenceStatus.Reserved, activeReservationId: "res-2"),
            SupportAgent("mismatch", AgentPresenceStatus.Offline),
            SupportAgent("unrecorded", AgentPresenceStatus.Available),
            new()
            {
                ItemId = "outsider",
                UserId = "user-outsider",
                UserName = "outsider",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = [OtherQueueId],
                AllowedQueueIds = [OtherQueueId],
            },
            new()
            {
                ItemId = "loner",
                UserId = "user-loner",
                UserName = "loner",
                PresenceStatus = AgentPresenceStatus.Available,
            },
        };

        var sessions = new List<AgentSession>
        {
            LiveSession("ghost", _campaignQueueId),
            new()
            {
                UserId = "user-absent2",
                IsOnline = true,
                ConnectionIds = ["c-absent2"],
                LastHeartbeatUtc = _now.AddMinutes(-30),
                QueueIds = [OtherQueueId],
            },
            LiveSession("ready", _campaignQueueId),
            LiveSession("transferrer", _campaignQueueId),
            LiveSession("full", SupportQueueId),
            LiveSession("stuck", SupportQueueId),
            LiveSession("ringing", SupportQueueId),
            LiveSession("unrecorded", SupportQueueId),
            LiveSession("outsider", OtherQueueId),
            LiveSession("loner"),
        };

        var events = new List<InteractionEvent>
        {
            AuditEvents.State("ghost", _now.AddHours(-3), AgentPresenceStatus.Reserved, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
            AuditEvents.State("absent1", _now.AddHours(-21), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            AuditEvents.State("absent2", _now.AddHours(-21), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),

            // Signed in at 08:00, waited two hours for a first call, took a second one later, and has been waiting since.
            AuditEvents.State("ready", _now.AddHours(-4), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            AuditEvents.State("ready", _now.AddHours(-2), AgentPresenceStatus.Available, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
            AuditEvents.State("ready", _now.AddMinutes(-90), AgentPresenceStatus.Busy, AgentPresenceStatus.Available, AgentStateChangeSources.WorkCompleted),
            AuditEvents.State("ready", _now.AddMinutes(-30), AgentPresenceStatus.Available, AgentPresenceStatus.Busy, AgentStateChangeSources.Accepted),
            AuditEvents.State("ready", _now.AddMinutes(-5), AgentPresenceStatus.Busy, AgentPresenceStatus.Available, AgentStateChangeSources.WorkCompleted),
            AuditEvents.State("transferrer", _now.AddHours(-1), AgentPresenceStatus.Busy, AgentPresenceStatus.Available, AgentStateChangeSources.WorkCompleted),
            AuditEvents.State("full", _now.AddHours(-1), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            AuditEvents.State("stuck", _now.AddMinutes(-20), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved),
            AuditEvents.State("ringing", _now.AddSeconds(-10), AgentPresenceStatus.Available, AgentPresenceStatus.Reserved, AgentStateChangeSources.Reserved),
            AuditEvents.State("mismatch", _now.AddHours(-2), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            AuditEvents.State("outsider", _now.AddHours(-1), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
            AuditEvents.State("loner", _now.AddHours(-1), AgentPresenceStatus.Offline, AgentPresenceStatus.Available, AgentStateChangeSources.SignIn),
        };

        var staleWork = new Dictionary<string, StaleAgentWork>(StringComparer.Ordinal)
        {
            ["ghost"] = new()
            {
                AgentId = "ghost",
                Items =
                [
                    new StaleAgentWorkItem
                    {
                        Kind = StaleAgentWorkKind.TelephonyCall,
                        Reason = "A phone call is still open although the provider cannot resolve it.",
                        ReferenceId = "call-1",
                        StartedUtc = _now.AddHours(-3),
                    },
                ],
            },
            ["stuck"] = new()
            {
                AgentId = "stuck",
                Items =
                [
                    new StaleAgentWorkItem
                    {
                        Kind = StaleAgentWorkKind.Reservation,
                        Reason = "The agent still holds a reservation that no offer or call backs.",
                        ReferenceId = "res-1",
                        StartedUtc = _now.AddMinutes(-20),
                    },
                    new StaleAgentWorkItem
                    {
                        Kind = StaleAgentWorkKind.Presence,
                        Reason = "The agent is marked busy with no call in progress.",
                        StartedUtc = _now.AddMinutes(-20),
                    },
                ],
            },
        };

        var head = new QueueItem
        {
            ItemId = "item-1",
            QueueId = _campaignQueueId,
            EnqueuedUtc = _now.AddMinutes(-2),
            ExcludedAgentIds = ["transferrer"],
        };

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);
        agentManager
            .Setup(manager => manager.GetAvailableForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string queueId, CancellationToken _) => agents
                .Where(agent => agent.PresenceStatus == AgentPresenceStatus.Available && AgentEntitlementUtilities.HasQueueEntitlement(agent, queueId))
                .ToArray());

        var sessionManager = new Mock<IAgentSessionManager>();
        sessionManager
            .Setup(manager => manager.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> userIds, CancellationToken _) => sessions
                .Where(session => userIds.Contains(session.UserId))
                .ToArray());

        var activeCounts = new Dictionary<string, int>(StringComparer.Ordinal) { ["full"] = 1 };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.CountActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeCounts);
        interactionManager
            .Setup(manager => manager.GetActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.GetByAggregateWindowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IEnumerable<string> _, IEnumerable<string> ids, DateTime fromUtc, DateTime throughUtc, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<InteractionEvent>>(events
                    .Where(interactionEvent => interactionEvent.OccurredUtc >= fromUtc && interactionEvent.OccurredUtc <= throughUtc)
                    .Where(interactionEvent => ids is null || ids.Contains(interactionEvent.AggregateId))
                    .OrderBy(interactionEvent => interactionEvent.OccurredUtc)
                    .ToArray()));
        eventStore
            .Setup(store => store.GetLatestBeforeAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, IEnumerable<string> _, IEnumerable<string> ids, DateTime beforeUtc, CancellationToken _) =>
                Task.FromResult<IReadOnlyList<InteractionEvent>>(events
                    .Where(interactionEvent => interactionEvent.OccurredUtc < beforeUtc && ids.Contains(interactionEvent.AggregateId))
                    .GroupBy(interactionEvent => interactionEvent.AggregateId)
                    .Select(group => group.OrderBy(interactionEvent => interactionEvent.OccurredUtc).Last())
                    .ToArray()));

        var queueManager = new Mock<IActivityQueueManager>();
        queueManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActivityQueue>
            {
                new() { ItemId = SupportQueueId, Name = "Support", Enabled = true },
                new() { ItemId = OtherQueueId, Name = "Other", Enabled = true },
            });

        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(manager => manager.CountWaitingByQueueIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>(StringComparer.Ordinal) { [_campaignQueueId] = 3 });
        queueItemManager
            .Setup(manager => manager.FindNextWaitingAsync(It.IsAny<ActivityQueue>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityQueue queue, DateTime _, CancellationToken _) => queue.ItemId == _campaignQueueId ? head : null);

        var businessHours = new Mock<IBusinessHoursService>();
        businessHours
            .Setup(service => service.IsOpenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var campaignManager = new Mock<ICatalogManager<OmnichannelCampaign>>();
        campaignManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OmnichannelCampaign> { new() { ItemId = CampaignId, DisplayText = "Spring promo" } });

        // The supervisor oversees the campaign and the support queue, not the other queue.
        var supervisorAuthorization = new Mock<ISupervisorQueueAuthorizationService>();
        supervisorAuthorization
            .Setup(service => service.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), SupervisorUserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal _, string _, string queueId, CancellationToken _) =>
                queueId == _campaignQueueId || queueId == SupportQueueId);

        var staleWorkService = new Mock<IStaleAgentWorkService>();
        staleWorkService
            .Setup(service => service.DetectAsync(It.IsAny<IReadOnlyCollection<AgentProfile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleWork);

        var capabilityGuard = new Mock<IContactCenterReportCapabilityGuard>();
        capabilityGuard
            .Setup(guard => guard.GetMissingFeaturesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var availabilityOptions = Options.Create(new AgentAvailabilityOptions());
        var availabilityService = new AgentAvailabilityService(
            agentManager.Object,
            sessionManager.Object,
            interactionManager.Object,
            availabilityOptions,
            clock.Object);
        var routingService = new ActivityRoutingService(
        [
            new RequiredSkillsRoutingStrategy(clock.Object),
            new CapacityRoutingStrategy(),
            new LongestIdleRoutingStrategy(),
        ]);

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, SupervisorUserId)], "Test")),
            },
        };

        var provider = new AgentAvailabilityReportProvider(
            Mock.Of<IContactCenterReportingService>(),
            capabilityGuard.Object,
            agentManager.Object,
            sessionManager.Object,
            interactionManager.Object,
            eventStore.Object,
            queueManager.Object,
            queueItemManager.Object,
            businessHours.Object,
            availabilityService,
            routingService,
            supervisorAuthorization.Object,
            campaignManager.Object,
            withStaleWorkDetection ? new[] { staleWorkService.Object } : Array.Empty<IStaleAgentWorkService>(),
            httpContextAccessor,
            availabilityOptions,
            clock.Object,
            Mock.Of<ILocalClock>(),
            new FormattingLocalizer<AgentAvailabilityReportProvider>());

        var filter = new ReportFilter();
        ContactCenterReportFilter.SetString(filter, ContactCenterReportFilter.AgentId, agentId);

        return await provider.RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);
    }

    private static AgentProfile CampaignAgent(
        string id,
        AgentPresenceStatus status,
        DateTime? presenceChangedUtc = null,
        DateTime? idleSinceUtc = null)
        => new()
        {
            ItemId = id,
            UserId = "user-" + id,
            UserName = id,
            PresenceStatus = status,
            PresenceChangedUtc = presenceChangedUtc,
            IdleSinceUtc = idleSinceUtc,
            QueueIds = [_campaignQueueId],
            AllowedQueueIds = [_campaignQueueId],
            CampaignIds = [CampaignId],
            AllowedCampaignIds = [CampaignId],
        };

    private static AgentProfile SupportAgent(string id, AgentPresenceStatus status, string activeReservationId = null)
        => new()
        {
            ItemId = id,
            UserId = "user-" + id,
            UserName = id,
            PresenceStatus = status,
            PresenceChangedUtc = _now.AddMinutes(-20),
            ActiveReservationId = activeReservationId,
            QueueIds = [SupportQueueId],
            AllowedQueueIds = [SupportQueueId],
        };

    private static AgentSession LiveSession(string agentId, params string[] queueIds)
        => new()
        {
            UserId = "user-" + agentId,
            IsOnline = true,
            ConnectionIds = ["c-" + agentId],
            LastHeartbeatUtc = _now.AddSeconds(-5),
            QueueIds = [.. queueIds],
        };

    private static ReportRow AgentRow(ReportDocument document, string userName)
        => Assert.Single(document.Sections[1].Rows, row => IsAgent(row, userName));

    private static ReportRow QueueRow(ReportDocument document, string userName, string queueName)
        => Assert.Single(document.Sections[2].Rows, row => IsAgent(row, userName) && row.Cells[1] == queueName);

    private static string[] AgentNames(ReportDocument document)
        => document.Sections[1].Rows
            .Select(row => ReportValue.TryGetUserName(row.Cells[0], out var userName) ? userName : row.Cells[0])
            .ToArray();

    private static bool IsAgent(ReportRow row, string userName)
        => ReportValue.TryGetUserName(row.Cells[0], out var name) && name == userName;

    private sealed class FormattingLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
