using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What the live dashboard's "Why isn't this dialing?" poll tells a supervisor: each queue and campaign they oversee that
/// has work pending, how much of it is dialable and how much is held back, and how many agents there are to take it.
/// </summary>
public sealed class SupervisorDashboardDispatchTests
{
    private const string SupervisorId = "supervisor-1";

    private static readonly string _renewalsQueue = ContactCenterConstants.CampaignQueue.CreateId("renewals");
    private static readonly string _winbackQueue = ContactCenterConstants.CampaignQueue.CreateId("winback");

    [Fact]
    public async Task DispatchPoll_ACampaignWithPendingWork_IsExplainedAndNamed()
    {
        // Arrange
        var probe = new DispatchProbe();
        probe.AddCampaign("renewals", "Renewals");
        probe.Authorize(_renewalsQueue);
        probe.Diagnose(new QueueDispatchDiagnostics
        {
            QueueId = _renewalsQueue,
            CampaignId = "renewals",
            PendingCount = 180,
            EvaluatedCount = 180,
            DialableNowCount = 0,
            HeldBackCount = 180,
            Buckets = [new DispatchBucket { Blocker = DispatchBlocker.ProfileDisabled, Count = 180 }],
        });

        // Act
        var model = await probe.PollAsync();

        // Assert
        var row = Assert.Single(model.Queues);
        Assert.Equal("Renewals", row.Name);
        Assert.Equal("renewals", row.CampaignId);
        Assert.Equal(180, row.PendingCount);
        Assert.Equal(180, row.HeldBackCount);
        var bucket = Assert.Single(row.Buckets);
        Assert.Equal("ProfileDisabled", bucket.Blocker);
        Assert.Equal(180, bucket.Count);
    }

    // Available agents signed in and nothing assigned is the case this exists for: the row says how many there are.
    [Fact]
    public async Task DispatchPoll_ReportsHowManyAgentsAreSignedInToTheCampaignAndHowManyAreAvailable()
    {
        // Arrange
        var probe = new DispatchProbe();
        probe.AddCampaign("renewals", "Renewals");
        probe.Authorize(_renewalsQueue);
        probe.AddAgent(new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available, CampaignIds = ["renewals"] });
        probe.AddAgent(new AgentProfile { ItemId = "a2", UserId = "u2", PresenceStatus = AgentPresenceStatus.Busy, CampaignIds = ["renewals"] });
        probe.AddAgent(new AgentProfile { ItemId = "a3", UserId = "u3", PresenceStatus = AgentPresenceStatus.Available, CampaignIds = ["winback"] });
        probe.Diagnose(Pending(_renewalsQueue, 10));

        // Act
        var model = await probe.PollAsync();

        // Assert
        var row = Assert.Single(model.Queues);
        Assert.Equal(2, row.SignedInAgentCount);
        Assert.Equal(1, row.AvailableAgentCount);
    }

    [Fact]
    public async Task DispatchPoll_ACampaignTheSupervisorDoesNotOversee_IsNotExplained()
    {
        // Arrange
        var probe = new DispatchProbe();
        probe.AddCampaign("renewals", "Renewals");
        probe.AddCampaign("winback", "Win-back");
        probe.Authorize(_renewalsQueue);
        probe.Diagnose(Pending(_renewalsQueue, 10), Pending(_winbackQueue, 99));

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Equal("renewals", Assert.Single(model.Queues).CampaignId);
        Assert.DoesNotContain(probe.DiagnosedQueueIds, queueId => queueId == _winbackQueue);
    }

    [Fact]
    public async Task DispatchPoll_ACampaignWithNothingPending_IsLeftOut()
    {
        // Arrange
        var probe = new DispatchProbe();
        probe.AddCampaign("renewals", "Renewals");
        probe.Authorize(_renewalsQueue);
        probe.Diagnose(Pending(_renewalsQueue, 0));

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Empty(model.Queues);
    }

    // A campaign with work pending and nobody signed in is the one most worth explaining, so it is not found through its
    // agents.
    [Fact]
    public async Task DispatchPoll_ACampaignWithNobodySignedIn_IsStillExplained()
    {
        // Arrange
        var probe = new DispatchProbe();
        probe.AddCampaign("renewals", "Renewals");
        probe.Authorize(_renewalsQueue);
        probe.Diagnose(Pending(_renewalsQueue, 40));

        // Act
        var model = await probe.PollAsync();

        // Assert
        var row = Assert.Single(model.Queues);
        Assert.Equal(0, row.SignedInAgentCount);
        Assert.Equal(40, row.PendingCount);
    }

    [Fact]
    public async Task DispatchPoll_WhenRoutingFoundNoAgent_CarriesWhatItFound()
    {
        // Arrange
        var probe = new DispatchProbe();
        probe.AddCampaign("renewals", "Renewals");
        probe.Authorize(_renewalsQueue);

        var diagnosis = Pending(_renewalsQueue, 10);
        diagnosis.RoutingFinding = new DispatchRoutingFinding
        {
            OccurredUtc = new DateTime(2026, 10, 15, 14, 58, 0, DateTimeKind.Utc),
            Reason = "No eligible agent was found.",
            CandidateCount = 3,
            CandidateReasons = [new DispatchCandidateReason { Reason = "Agent is not available.", AgentCount = 3 }],
        };
        probe.Diagnose(diagnosis);

        // Act
        var model = await probe.PollAsync();

        // Assert
        var routing = Assert.IsType<SupervisorDispatchRoutingViewModel>(Assert.Single(model.Queues).Routing);
        Assert.Equal("No eligible agent was found.", routing.Reason);
        Assert.Equal(3, routing.CandidateCount);
        var reason = Assert.Single(routing.CandidateReasons);
        Assert.Equal("Agent is not available.", reason.Reason);
        Assert.Equal(3, reason.AgentCount);
    }

    // The breakdown reads a campaign's whole pending inventory; a supervisor's polls share one reading.
    [Fact]
    public async Task DispatchPoll_ASecondPollSoonAfterTheFirst_DoesNotDiagnoseAgain()
    {
        // Arrange
        var probe = new DispatchProbe();
        probe.AddCampaign("renewals", "Renewals");
        probe.Authorize(_renewalsQueue);
        probe.Diagnose(Pending(_renewalsQueue, 10));

        // Act
        await probe.PollAsync();
        var second = await probe.PollAsync();

        // Assert
        Assert.Equal("renewals", Assert.Single(second.Queues).CampaignId);
        probe.Diagnostics.Verify(
            service => service.DiagnoseAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchPoll_WithoutThePermissionToMonitor_IsForbidden()
    {
        // Arrange
        var probe = new DispatchProbe { Allowed = false };

        // Act
        var result = await probe.PollRawAsync();

        // Assert
        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(result);
        probe.Diagnostics.Verify(
            service => service.DiagnoseAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static QueueDispatchDiagnostics Pending(string queueId, int pending)
        => new()
        {
            QueueId = queueId,
            CampaignId = ContactCenterConstants.CampaignQueue.GetCampaignId(queueId),
            PendingCount = pending,
            EvaluatedCount = pending,
            DialableNowCount = pending,
            Buckets = pending == 0 ? [] : [new DispatchBucket { Blocker = DispatchBlocker.DialableNow, Count = pending }],
        };

    private sealed class DispatchProbe
    {
        private readonly List<AgentProfile> _agents = [];
        private readonly List<OmnichannelCampaign> _campaigns = [];
        private readonly Dictionary<string, QueueDispatchDiagnostics> _diagnoses = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _authorizedQueueIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

        public DispatchProbe()
        {
            Diagnostics
                .Setup(service => service.DiagnoseAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> queueIds, CancellationToken _) =>
                {
                    DiagnosedQueueIds.AddRange(queueIds);

                    return queueIds
                        .Select(queueId => _diagnoses.TryGetValue(queueId, out var diagnosis) ? diagnosis : new QueueDispatchDiagnostics { QueueId = queueId })
                        .ToArray();
                });
        }

        public bool Allowed { get; set; } = true;

        public Mock<IDialerDispatchDiagnosticsService> Diagnostics { get; } = new();

        public List<string> DiagnosedQueueIds { get; } = [];

        public void AddAgent(AgentProfile agent)
            => _agents.Add(agent);

        public void AddCampaign(string id, string name)
            => _campaigns.Add(new OmnichannelCampaign { ItemId = id, DisplayText = name });

        public void Authorize(params string[] queueIds)
            => _authorizedQueueIds.UnionWith(queueIds);

        public void Diagnose(params QueueDispatchDiagnostics[] diagnoses)
        {
            foreach (var diagnosis in diagnoses)
            {
                _diagnoses[diagnosis.QueueId] = diagnosis;
            }
        }

        public async Task<SupervisorDispatchStateViewModel> PollAsync()
        {
            var result = await PollRawAsync();
            var value = result.GetType().GetProperty("Value")?.GetValue(result);

            return Assert.IsType<SupervisorDispatchStateViewModel>(value);
        }

        public async Task<IResult> PollRawAsync()
        {
            var agentManager = new Mock<IAgentProfileManager>();
            agentManager
                .Setup(manager => manager.PageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<QueryContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PageResult<AgentProfile> { Count = _agents.Count, Entries = _agents.ToArray() });

            var queueManager = new Mock<IActivityQueueManager>();
            queueManager
                .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var campaignManager = new Mock<ICatalogManager<OmnichannelCampaign>>();
            campaignManager
                .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_campaigns);

            var queueAuthorization = new Mock<ISupervisorQueueAuthorizationService>();
            queueAuthorization
                .Setup(service => service.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), SupervisorId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ClaimsPrincipal _, string _, string queueId, CancellationToken _) => _authorizedQueueIds.Contains(queueId));

            return await SupervisorDashboardEndpoints.HandleDispatchAsync(
                new ConfigurableAuthorizationService(Allowed),
                queueManager.Object,
                agentManager.Object,
                queueAuthorization.Object,
                Diagnostics.Object,
                _cache,
                new StubClock(),
                new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, SupervisorId)], "Test")),
                    RequestServices = new ServiceCollection()
                        .AddSingleton(campaignManager.Object)
                        .BuildServiceProvider(),
                });
        }
    }

    private sealed class ConfigurableAuthorizationService : IAuthorizationService
    {
        private readonly bool _allowed;

        public ConfigurableAuthorizationService(bool allowed)
        {
            _allowed = allowed;
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(_allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(_allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
    }
}
