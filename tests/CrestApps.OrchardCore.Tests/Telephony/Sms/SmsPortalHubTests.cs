using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Hubs;
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// Pins what connecting to the SMS portal puts an agent into.
/// </summary>
/// <remarks>
/// <para>
/// The connection is the agent's presence and their subscription at the same time: the groups joined here decide
/// which inbound messages reach their inbox, and the presence touch decides whether routed assignment will push
/// a conversation at them at all. An agent who connects but lands in the wrong groups stays online, looks
/// available, and silently receives nothing.
/// </para>
/// <para>
/// The group manager here is hand-rolled rather than a mock with verifications, because
/// <c>HubCancellationConventionTests</c> scans every source and test file for group-manager calls and pins the
/// exact set of files allowed to make them.
/// </para>
/// </remarks>
public sealed class SmsPortalHubTests
{
    private const string ConnectionId = "connection-1";
    private const string UserId = "user-1";
    private const string AgentId = "agent-1";
    private const string TenantName = "tenant-1";

    /// <summary>
    /// Spells a recorded group join the way the suite qualifies group names by tenant.
    /// </summary>
    /// <param name="groupName">The logical group name.</param>
    /// <returns>The recorded operation to expect.</returns>
    private static string Group(string groupName)
        => "add:" + TenantSignalRGroupName.ForGroup(TenantName, groupName);

    [Fact]
    public async Task OnConnectedAsync_WithoutPortalAccess_AbortsAndJoinsNothing()
    {
        // Arrange
        var harness = new Harness(canUsePortal: false);

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert: refusing the connection is the whole protection. A caller who is let through joins groups
        // carrying other people's conversations.
        Assert.Equal(1, harness.Context.AbortCount);
        Assert.Empty(harness.Groups.Operations);
        Assert.Empty(harness.Touched);
    }

    [Fact]
    public async Task OnConnectedAsync_WithoutAnHttpContext_Aborts()
    {
        // Arrange
        var harness = new Harness(canUsePortal: true, withHttpContext: false);

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Equal(1, harness.Context.AbortCount);
        Assert.Empty(harness.Groups.Operations);
    }

    [Fact]
    public async Task OnConnectedAsync_JoinsTheAgentGroupAndEveryQueueTheyServe()
    {
        // Arrange
        var harness = new Harness(canUsePortal: true, agent: new AgentProfile
        {
            ItemId = AgentId,
            QueueIds = ["queue-a"],
            AllowedQueueIds = ["queue-b"],
        });

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert: every group name is tenant-qualified, so two tenants on one backplane never share a group.
        Assert.Equal(
            [
                Group("sms:agent:agent-1"),
                Group("sms:queue:queue-a"),
                Group("sms:queue:queue-b"),
            ],
            harness.Groups.Operations);

        Assert.Equal([AgentId], harness.Touched);
    }

    [Fact]
    public async Task OnConnectedAsync_WhenAQueueIsBothServedAndAllowed_JoinsItOnce()
    {
        // Arrange
        var harness = new Harness(canUsePortal: true, agent: new AgentProfile
        {
            ItemId = AgentId,
            QueueIds = ["queue-a"],
            AllowedQueueIds = ["queue-a"],
        });

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Equal(
            [Group("sms:agent:agent-1"), Group("sms:queue:queue-a")],
            harness.Groups.Operations);
    }

    [Fact]
    public async Task OnConnectedAsync_ForASupervisor_AlsoJoinsTheTriageGroup()
    {
        // Arrange
        var harness = new Harness(
            canUsePortal: true,
            canViewAll: true,
            agent: new AgentProfile { ItemId = AgentId });

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Contains(Group(SmsPortalHub.UnassignedGroup), harness.Groups.Operations);
    }

    [Fact]
    public async Task OnConnectedAsync_ForSomebodyWhoCannotViewEverything_DoesNotJoinTheTriageGroup()
    {
        // Arrange
        var harness = new Harness(
            canUsePortal: true,
            canViewAll: false,
            agent: new AgentProfile { ItemId = AgentId });

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert: the triage group carries conversations nobody owns yet, which an ordinary agent may not see.
        Assert.DoesNotContain(Group(SmsPortalHub.UnassignedGroup), harness.Groups.Operations);
    }

    [Fact]
    public async Task OnConnectedAsync_WithNoAgentProfile_JoinsNoAgentOrQueueGroup()
    {
        // Arrange
        var harness = new Harness(canUsePortal: true, agent: null);

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert: somebody who holds the permission but is not an agent has no inbox to subscribe to.
        Assert.Empty(harness.Groups.Operations);
        Assert.Empty(harness.Touched);
    }

    [Fact]
    public async Task Heartbeat_RecordsThePortalAsStillOpen()
    {
        // Arrange
        var harness = new Harness(canUsePortal: true, agent: new AgentProfile { ItemId = AgentId });

        // Act
        await harness.Hub.Heartbeat();

        // Assert: when the check-ins stop, routed assignment stops pushing work at an agent who is not there.
        Assert.Equal([AgentId], harness.Touched);
    }

    [Fact]
    public async Task Heartbeat_WithNoAgentProfile_RecordsNothing()
    {
        // Arrange
        var harness = new Harness(canUsePortal: true, agent: null);

        // Act
        await harness.Hub.Heartbeat();

        // Assert
        Assert.Empty(harness.Touched);
    }

    private sealed class Harness
    {
        public Harness(
            bool canUsePortal,
            bool canViewAll = false,
            AgentProfile agent = null,
            bool withHttpContext = true)
        {
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test"));

            Context = new TestHubCallerContext(principal, withHttpContext);

            var agentProfileManager = new Mock<IAgentProfileManager>();
            agentProfileManager
                .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agent);

            var presenceTracker = new Mock<ISmsAgentPresenceTracker>();
            presenceTracker
                .Setup(tracker => tracker.TouchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string agentId, CancellationToken _) =>
                {
                    Touched.Add(agentId);

                    return Task.CompletedTask;
                });

            Hub = new SmsPortalHub(
                agentProfileManager.Object,
                new ScriptedAuthorizationService(canUsePortal, canViewAll),
                presenceTracker.Object,
                new FakeTenantAccessor(TenantName))
            {
                Context = Context,
                Groups = Groups,
            };
        }

        public SmsPortalHub Hub { get; }

        public TestHubCallerContext Context { get; }

        public RecordingGroupManager Groups { get; } = new();

        public List<string> Touched { get; } = [];
    }

    /// <summary>
    /// Answers the two questions the hub asks, without a permission system behind it.
    /// </summary>
    private sealed class ScriptedAuthorizationService : IAuthorizationService
    {
        private readonly bool _canUsePortal;
        private readonly bool _canViewAll;

        public ScriptedAuthorizationService(bool canUsePortal, bool canViewAll)
        {
            _canUsePortal = canUsePortal;
            _canViewAll = canViewAll;
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            // Matched on the requirement's own name so the same double works whether the hub asks for an
            // Orchard permission or a framework authorization operation.
            var wantsViewAll = requirements.Any(requirement => IsViewAll(NameOf(requirement)));

            var allowed = wantsViewAll ? _canViewAll : _canUsePortal;

            return Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());

        private static string NameOf(IAuthorizationRequirement requirement) => requirement switch
        {
            PermissionRequirement permission => permission.Permission.Name,
            OperationAuthorizationRequirement operation => operation.Name,
            _ => null,
        };

        /// <summary>
        /// Whether a requirement is the supervisor "see every conversation" question.
        /// </summary>
        /// <remarks>
        /// Two spellings, because the permission carries its stored id and the operation carries its own name.
        /// Matching both keeps this double honest across the change from one to the other.
        /// </remarks>
        /// <param name="name">The requirement's name.</param>
        /// <returns><see langword="true"/> when it is the view-everything question.</returns>
        private static bool IsViewAll(string name)
            => name is "ViewAllConversations" or "ViewAllSmsConversations";
    }

    private sealed class TestHubCallerContext : HubCallerContext
    {
        public TestHubCallerContext(ClaimsPrincipal principal, bool withHttpContext)
        {
            User = principal;
            Features = new FeatureCollection();

            if (withHttpContext)
            {
                Features.Set<IHttpContextFeature>(new TestHttpContextFeature
                {
                    HttpContext = new DefaultHttpContext { User = principal },
                });
            }
        }

        public int AbortCount { get; private set; }

        public CancellationTokenSource ConnectionAbortedSource { get; } = new();

        public override string ConnectionId => SmsPortalHubTests.ConnectionId;

        public override string UserIdentifier => UserId;

        public override ClaimsPrincipal User { get; }

        public override IDictionary<object, object> Items { get; } = new Dictionary<object, object>();

        public override IFeatureCollection Features { get; }

        public override CancellationToken ConnectionAborted => ConnectionAbortedSource.Token;

        public override void Abort() => AbortCount++;
    }

    private sealed class TestHttpContextFeature : IHttpContextFeature
    {
        public HttpContext HttpContext { get; set; }
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<string> Operations { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Assert.Equal(ConnectionId, connectionId);
            cancellationToken.ThrowIfCancellationRequested();
            Operations.Add($"add:{groupName}");

            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Assert.Equal(ConnectionId, connectionId);
            cancellationToken.ThrowIfCancellationRequested();
            Operations.Add($"remove:{groupName}");

            return Task.CompletedTask;
        }
    }
}
